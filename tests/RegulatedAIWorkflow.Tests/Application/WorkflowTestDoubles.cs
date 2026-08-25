using RegulatedAIWorkflow.Core.Application;
using RegulatedAIWorkflow.Core.Application.Approval;
using RegulatedAIWorkflow.Core.Application.Workflow;
using RegulatedAIWorkflow.Core.Contracts.Approval;
using RegulatedAIWorkflow.Core.Contracts.Audit;
using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Approval;
using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Execution;
using RegulatedAIWorkflow.Core.Domain.Risk;
using RegulatedAIWorkflow.Core.Ports;
using RegulatedAIWorkflow.Infrastructure.Approval;
using RegulatedAIWorkflow.Infrastructure.Audit;
using RegulatedAIWorkflow.Infrastructure.Evidence;
using RegulatedAIWorkflow.Infrastructure.Execution;

namespace RegulatedAIWorkflow.Tests.Application;

internal sealed class WorkflowTestHarness
{
    internal static readonly DateTimeOffset ExpectedUtcNow =
        new(2026, 8, 21, 10, 30, 0, TimeSpan.Zero);

    internal FixedTimeProvider TimeProvider { get; } = new(ExpectedUtcNow);

    internal InMemoryAuditSink AuditSink { get; } = new();

    internal InMemoryApprovalRepository ApprovalRepository { get; } = new();

    internal InMemoryActionExecutor ActionExecutor { get; } = new();

    internal WorkflowOrchestrator CreateOrchestrator(
        IEvidenceRepository? evidenceRepository = null,
        IRiskEvaluator? riskEvaluator = null,
        IAuditSink? auditSink = null,
        IApprovalRepository? approvalRepository = null,
        IActionExecutor? actionExecutor = null,
        TimeProvider? timeProvider = null)
    {
        var approvals = approvalRepository ?? ApprovalRepository;
        var clock = timeProvider ?? TimeProvider;
        return new(
            evidenceRepository ?? new InMemoryEvidenceRepository(),
            riskEvaluator ?? new DeterministicRiskEvaluator(),
            new ApprovalGate(approvals, clock),
            auditSink ?? AuditSink,
            actionExecutor ?? ActionExecutor,
            clock);
    }

    internal ApprovalIssuer CreateApprovalIssuer(
        IEvidenceRepository? evidenceRepository = null,
        IRiskEvaluator? riskEvaluator = null,
        IApprovalRepository? approvalRepository = null,
        IAuditSink? auditSink = null,
        TimeProvider? timeProvider = null) =>
        new(
            evidenceRepository ?? new InMemoryEvidenceRepository(),
            riskEvaluator ?? new DeterministicRiskEvaluator(),
            approvalRepository ?? ApprovalRepository,
            auditSink ?? AuditSink,
            timeProvider ?? TimeProvider);

    internal Task<ApprovalIssueResult> IssueApprovalAsync(
        WorkflowPrincipal? approver = null,
        string vendorId = "silverline-payments",
        int validForHours = 24) =>
        CreateApprovalIssuer().IssueAsync(
            approver ?? Principal(UserRole.RiskApprover, userId: "risk-approver"),
            new IssueApprovalCommand(
                vendorId,
                WorkflowAction.MarkVendorApproved,
                validForHours));

    internal static WorkflowPrincipal Principal(
        UserRole role = UserRole.ProcurementManager,
        string tenantId = "northstar-bank",
        string userId = "procurement-user") =>
        new(tenantId, userId, role);

    internal static WorkflowCommand Command(
        string? vendorId = "silverline-payments",
        string? question = null,
        WorkflowAction action = WorkflowAction.MarkVendorApproved,
        string? approvalId = null) =>
        new(vendorId, question, action, approvalId);

    internal static EvidenceSearchResult Evidence(string snippet = "Trusted display evidence.") =>
        new(
            [Document("policy-document", snippet)],
            [Fact("policy-document", EvidenceFactType.SecurityEvidenceRequired)]);

    internal static EvidenceDocument Document(
        string documentId,
        string snippet,
        string tenantId = "northstar-bank",
        string vendorId = "silverline-payments") =>
        new(
            documentId,
            tenantId,
            vendorId,
            EvidenceDocumentType.Policy,
            UntrustedText.FromExternalSource(snippet));

    internal static EvidenceFact Fact(
        string sourceDocumentId,
        EvidenceFactType factType,
        string tenantId = "northstar-bank",
        string vendorId = "silverline-payments") =>
        new(tenantId, vendorId, sourceDocumentId, factType);

    internal static RiskEvaluation HighEvaluation(
        IReadOnlyList<RiskCitationReference>? references = null,
        bool evidenceIsAmbiguous = false,
        string policyVersion = "test-policy-1") =>
        new(
            RiskLevel.High,
            "Do not approve yet.",
            [new RiskReason("TEST_HIGH", "A trusted policy rule blocked the action.")],
            references ?? [],
            [new MissingEvidenceItem("TEST_CONTROL", "A required control is missing.")],
            RequiresApproval: true,
            evidenceIsAmbiguous,
            PolicyVersion: policyVersion);

    internal static RiskEvaluation MediumEvaluation() =>
        new(
            RiskLevel.Medium,
            "Proceed only with standard controls.",
            [],
            [],
            [],
            RequiresApproval: false,
            EvidenceIsAmbiguous: false,
            PolicyVersion: "test-policy-1");
}

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset currentUtc = utcNow;

    public override DateTimeOffset GetUtcNow() => currentUtc;

    internal void Advance(TimeSpan duration) => currentUtc = currentUtc.Add(duration);
}

internal sealed class StubEvidenceRepository(
    Func<EvidenceQuery, CancellationToken, Task<EvidenceSearchResult>> handler,
    IList<string>? sequence = null) : IEvidenceRepository
{
    internal int CallCount { get; private set; }

    public Task<EvidenceSearchResult> SearchEvidenceAsync(
        EvidenceQuery query,
        CancellationToken cancellationToken)
    {
        CallCount++;
        sequence?.Add("retrieve");
        return handler(query, cancellationToken);
    }
}

internal sealed class StubRiskEvaluator(
    Func<RiskEvaluationInput, RiskEvaluation> handler,
    IList<string>? sequence = null) : IRiskEvaluator
{
    internal int CallCount { get; private set; }

    public RiskEvaluation EvaluateRisk(RiskEvaluationInput input)
    {
        CallCount++;
        sequence?.Add("evaluate");
        return handler(input);
    }
}

internal sealed class SequencedAuditSink(
    IAuditSink inner,
    IList<string> sequence) : IAuditSink
{
    public async Task WriteAuditEventAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken)
    {
        sequence.Add($"audit:{auditEvent.EventType}:{auditEvent.Outcome}");
        await inner.WriteAuditEventAsync(auditEvent, cancellationToken);
    }
}

internal sealed class ThrowingAuditSink(Exception exception) : IAuditSink
{
    internal int CallCount { get; private set; }

    public Task WriteAuditEventAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken)
    {
        CallCount++;
        throw exception;
    }
}

internal sealed class RecordingActionExecutor(IList<string>? sequence = null) : IActionExecutor
{
    internal List<ActionExecutionRequest> Executions { get; } = [];

    internal int CallCount => Executions.Count;

    internal bool Succeeds { get; set; } = true;

    internal Exception? ExceptionToThrow { get; set; }

    public Task<ActionExecutionResult> ExecuteAsync(
        ActionExecutionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Executions.Add(request);
        sequence?.Add("execute");
        if (ExceptionToThrow is not null)
        {
            return Task.FromException<ActionExecutionResult>(ExceptionToThrow);
        }

        return Task.FromResult(new ActionExecutionResult(Succeeds));
    }
}

internal sealed class LeakyApprovalRepository(ApprovalRecord approval) : IApprovalRepository
{
    public Task SaveAsync(ApprovalRecord record, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<ApprovalRecord?> FindAsync(
        string tenantId,
        string approvalId,
        CancellationToken cancellationToken) =>
        Task.FromResult<ApprovalRecord?>(approval);
}
