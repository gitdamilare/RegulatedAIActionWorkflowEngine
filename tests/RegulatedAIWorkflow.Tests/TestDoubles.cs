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
    internal const string Requester = "procurement-user";
    internal const string Approver = "risk-approver";

    internal CountingEvidenceRepository Evidence { get; } = new(new InMemoryEvidenceRepository());

    internal InMemoryApprovalRepository Approvals { get; } = new();

    internal InMemoryAuditSink Audit { get; } = new();

    internal RecordingActionExecutor Executor { get; } = new();

    internal WorkflowOrchestrator Orchestrator(IEvidenceRepository? evidence = null) =>
        new(
            evidence ?? Evidence,
            new DeterministicRiskEvaluator(),
            new ApprovalGate(Approvals),
            Audit,
            Executor,
            new FixedTimeProvider(Now));

    internal ApprovalIssuer Issuer() => new(Approvals, new FixedTimeProvider(Now));

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
        string approverUserId = Approver)
    {
        var result = await Issuer().IssueAsync(
            new WorkflowPrincipal(tenantId, approverUserId, UserRole.RiskApprover),
            vendorId,
            action,
            CancellationToken.None);
        return result.Approval!;
    }
}

/// <summary>Records every effect and can be told to fail, which is how the unknown-outcome path is reached.</summary>
internal sealed class RecordingActionExecutor : IActionExecutor
{
    private readonly List<Core.Domain.Execution.ActionExecutionRequest> requests = [];

    internal IReadOnlyList<Core.Domain.Execution.ActionExecutionRequest> Requests => requests;

    internal int CallCount => requests.Count;

    internal Exception? ExceptionToThrow { get; set; }

    public Task ExecuteAsync(
        Core.Domain.Execution.ActionExecutionRequest request,
        CancellationToken cancellationToken)
    {
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
