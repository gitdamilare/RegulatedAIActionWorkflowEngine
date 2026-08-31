using RegulatedAIWorkflow.Core.Application;
using RegulatedAIWorkflow.Core.Application.Approval;
using RegulatedAIWorkflow.Core.Application.Workflow;
using RegulatedAIWorkflow.Core.Contracts.Approval;
using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Ports;
using RegulatedAIWorkflow.Infrastructure.Approval;
using RegulatedAIWorkflow.Infrastructure.Audit;
using RegulatedAIWorkflow.Infrastructure.Evidence;

namespace RegulatedAIWorkflow.Tests;

/// <summary>
/// Wires the real in-memory adapters, so tests exercise shipped behaviour rather than a parallel fake.
/// Only the executor is a spy, because "was the effect performed" is the assertion that matters most.
/// </summary>
internal sealed class Harness
{
    internal static readonly DateTimeOffset Now = new(2026, 8, 30, 10, 30, 0, TimeSpan.Zero);

    internal const string TenantA = "northstar-bank";
    internal const string TenantB = "harborview-bank";
    internal const string Vendor = "silverline-payments";
    internal const string TenantAOnlyVendor = "lakeshore-analytics";
    internal const string LowRiskVendor = "brightpath-print";
    internal const string Requester = "procurement-user";
    internal const string Approver = "risk-approver";

    internal CountingEvidenceRepository Evidence { get; } = new(new InMemoryEvidenceRepository());

    internal InMemoryApprovalRepository Approvals { get; } = new();

    internal InMemoryAuditSink Audit { get; } = new();

    /// <summary>Records audit writes and effects in the order they actually happened.</summary>
    internal SequenceLog Sequence { get; } = new();

    internal RecordingActionExecutor Executor { get; }

    internal Harness() => Executor = new RecordingActionExecutor(Sequence);

    internal WorkflowOrchestrator Orchestrator(
        IEvidenceRepository? evidence = null,
        DateTimeOffset? at = null,
        IRiskEvaluator? riskEvaluator = null,
        IAuditSink? auditSink = null) =>
        new(
            evidence ?? Evidence,
            riskEvaluator ?? new DeterministicRiskEvaluator(),
            new ApprovalGate(Approvals, new FixedTimeProvider(at ?? Now)),
            auditSink ?? new SequencedAuditSink(Audit, Sequence),
            Executor,
            new FixedTimeProvider(at ?? Now));

    internal ApprovalIssuer Issuer(IEvidenceRepository? evidence = null) =>
        new(Approvals, evidence ?? Evidence, new FixedTimeProvider(Now));

    internal static WorkflowPrincipal Principal(
        string tenantId = TenantA,
        string userId = Requester,
        UserRole role = UserRole.ProcurementManager) =>
        new(tenantId, userId, role);

    internal static WorkflowCommand Command(
        string? vendorId = Vendor,
        string? approvalId = null,
        string? question = null,
        WorkflowAction action = WorkflowAction.MarkVendorApproved) =>
        new(vendorId, question, action, approvalId);

    /// <summary>Issues a real approval through the real issuer, as an approver distinct from the requester.</summary>
    internal async Task<ApprovalRecord> IssueApprovalAsync(
        string tenantId = TenantA,
        string vendorId = Vendor,
        WorkflowAction action = WorkflowAction.MarkVendorApproved,
        string approverUserId = Approver,
        IEvidenceRepository? evidence = null)
    {
        var result = await Issuer(evidence).IssueAsync(
            new WorkflowPrincipal(tenantId, approverUserId, UserRole.RiskApprover),
            vendorId,
            action,
            CancellationToken.None);
        return result.Approval!;
    }
}

/// <summary>Records every effect and can be told to fail, which is how the unknown-outcome path is reached.</summary>
internal sealed class RecordingActionExecutor(SequenceLog sequence) : IActionExecutor
{
    private readonly List<Core.Domain.Execution.ActionExecutionRequest> requests = [];

    internal IReadOnlyList<Core.Domain.Execution.ActionExecutionRequest> Requests => requests;

    internal int CallCount => requests.Count;

    internal Exception? ExceptionToThrow { get; set; }

    public Task ExecuteAsync(
        Core.Domain.Execution.ActionExecutionRequest request,
        CancellationToken cancellationToken)
    {
        sequence.Add("execute");
        requests.Add(request);
        return ExceptionToThrow is null ? Task.CompletedTask : Task.FromException(ExceptionToThrow);
    }
}

/// <summary>Counts retrievals, so a test can prove authorization ran before any evidence was touched.</summary>
internal sealed class CountingEvidenceRepository(IEvidenceRepository inner) : IEvidenceRepository
{
    internal int CallCount { get; private set; }

    internal Exception? ExceptionToThrow { get; set; }

    public Task<IReadOnlyList<EvidenceDocument>> SearchEvidenceAsync(
        EvidenceQuery query,
        CancellationToken cancellationToken)
    {
        CallCount++;
        return ExceptionToThrow is null
            ? inner.SearchEvidenceAsync(query, cancellationToken)
            : Task.FromException<IReadOnlyList<EvidenceDocument>>(ExceptionToThrow);
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

/// <summary>
/// Serves the real corpus after a transform, which is how evidence is made to move underneath an
/// approval that was already granted.
/// </summary>
internal sealed class TransformingEvidenceRepository(
    Func<IReadOnlyList<EvidenceDocument>, IReadOnlyList<EvidenceDocument>> transform) : IEvidenceRepository
{
    private readonly InMemoryEvidenceRepository inner = new();

    public async Task<IReadOnlyList<EvidenceDocument>> SearchEvidenceAsync(
        EvidenceQuery query,
        CancellationToken cancellationToken)
    {
        var documents = await inner.SearchEvidenceAsync(query, cancellationToken);
        return transform(documents);
    }
}

/// <summary>Returns a fixed assessment, so the orchestrator can be handed a result it must distrust.</summary>
internal sealed class StubRiskEvaluator(Core.Domain.Risk.RiskEvaluation evaluation) : IRiskEvaluator
{
    public Core.Domain.Risk.RiskEvaluation EvaluateRisk(Core.Domain.Risk.RiskEvaluationInput input) => evaluation;
}

/// <summary>An ordered log of the things a run did, so ordering can be asserted rather than assumed.</summary>
internal sealed class SequenceLog
{
    private readonly List<string> entries = [];

    internal IReadOnlyList<string> Entries => entries;

    internal void Add(string entry) => entries.Add(entry);
}

/// <summary>Writes through to the real sink, recording when each write happened relative to the effect.</summary>
internal sealed class SequencedAuditSink(InMemoryAuditSink inner, SequenceLog sequence) : IAuditSink
{
    public Task WriteAuditEventAsync(Core.Contracts.Audit.AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        sequence.Add($"audit:{auditEvent.EventType}:{auditEvent.Outcome}");
        return inner.WriteAuditEventAsync(auditEvent, cancellationToken);
    }
}

/// <summary>Fails one kind of audit write, so the pre-effect ordering can be shown to gate the effect.</summary>
internal sealed class FailingAuditSink(
    InMemoryAuditSink inner,
    Core.Contracts.Audit.AuditEventType failOnEventType) : IAuditSink
{
    public Task WriteAuditEventAsync(Core.Contracts.Audit.AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        return auditEvent.EventType == failOnEventType
            ? Task.FromException(new InvalidOperationException("The audit sink is unavailable."))
            : inner.WriteAuditEventAsync(auditEvent, cancellationToken);
    }
}
