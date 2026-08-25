using System.Text.Json;
using RegulatedAIWorkflow.Core.Contracts.Audit;
using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Risk;
using RegulatedAIWorkflow.Tests.Application;

namespace RegulatedAIWorkflow.Tests.Required;

/// <summary>
/// Required test 3: every denied, blocked, authorized, executed, and failed workflow path is safely audited.
/// Ordering assertions prove authorization is persisted before the mock side effect; this suite does not
/// claim durable, transactional, or tamper-evident audit guarantees.
/// </summary>
public sealed class Required_3_AuditTrailTests
{
    /// <summary>Verifies denied roles are audited without retrieving evidence or disclosing assessment data.</summary>
    [Theory]
    [InlineData(UserRole.Viewer)]
    [InlineData(UserRole.RiskApprover)]
    public async Task RunAsync_UnauthorizedRole_RetrievesNothingAndDisclosesNoAssessment(UserRole role)
    {
        var repository = RepositoryReturning(WorkflowTestHarness.Evidence());
        var evaluator = new StubRiskEvaluator(_ => WorkflowTestHarness.MediumEvaluation());
        var harness = new WorkflowTestHarness();

        var result = await harness.CreateOrchestrator(repository, evaluator).RunAsync(
            WorkflowTestHarness.Principal(role),
            WorkflowTestHarness.Command(question: "Does this vendor exist?"));

        repository.CallCount.ShouldBe(0);
        evaluator.CallCount.ShouldBe(0);
        harness.ActionExecutor.Executions.ShouldBeEmpty();
        result.ActionStatus.ShouldBe(ActionStatus.BlockedUnauthorized);
        AssertNoAssessment(result);
        harness.AuditSink.Events.Select(item => item.Outcome).ShouldBe(
            [AuditOutcome.BlockedUnauthorized, AuditOutcome.BlockedUnauthorized]);
    }

    /// <summary>Verifies successful dependencies and execution audit records run in order.</summary>
    [Fact]
    public async Task RunAsync_ValidRequest_UsesExpectedDependencyAndAuditOrder()
    {
        var sequence = new List<string>();
        var repository = new StubEvidenceRepository(
            (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(WorkflowTestHarness.Evidence());
            },
            sequence);
        var evaluator = new StubRiskEvaluator(_ => WorkflowTestHarness.MediumEvaluation(), sequence);
        var harness = new WorkflowTestHarness();
        var auditSink = new SequencedAuditSink(harness.AuditSink, sequence);
        var actionExecutor = new RecordingActionExecutor(sequence);

        var result = await harness.CreateOrchestrator(
            repository,
            evaluator,
            auditSink,
            actionExecutor: actionExecutor).RunAsync(
                WorkflowTestHarness.Principal(),
                WorkflowTestHarness.Command());

        result.ActionStatus.ShouldBe(ActionStatus.Executed);
        sequence.ShouldBe(
        [
            "retrieve",
            "evaluate",
            "audit:ActionAttempt:AuthorizedForExecution",
            "execute",
            "audit:ActionExecution:Executed",
            "audit:WorkflowCompleted:Executed"
        ]);
    }

    /// <summary>A reported executor failure proves no effect and returns the existing unavailable result.</summary>
    [Fact]
    public async Task RunAsync_ExecutorReportsNoEffect_ReturnsUnavailableAndAuditsOutcome()
    {
        var harness = new WorkflowTestHarness();
        var approval = await harness.IssueApprovalAsync();
        var sequence = new List<string>();
        var auditSink = new SequencedAuditSink(harness.AuditSink, sequence);
        var executor = new RecordingActionExecutor(sequence) { Succeeds = false };

        var result = await harness.CreateOrchestrator(
            auditSink: auditSink,
            actionExecutor: executor).RunAsync(
                WorkflowTestHarness.Principal(),
                WorkflowTestHarness.Command(approvalId: approval.ApprovalId));

        result.ActionStatus.ShouldBe(ActionStatus.BlockedExecutionUnavailable);
        result.RiskLevel.ShouldBe(RiskLevel.High);
        result.Citations.ShouldNotBeEmpty();
        result.Reasons.ShouldNotBeEmpty();
        sequence.ShouldBe(
        [
            "audit:ApprovalDecision:ApprovalAccepted",
            "audit:ActionAttempt:AuthorizedForExecution",
            "execute",
            "audit:ActionExecution:BlockedExecutionUnavailable",
            "audit:WorkflowCompleted:BlockedExecutionUnavailable"
        ]);

        var workflowEvents = harness.AuditSink.Events
            .Where(item => item.WorkflowId == result.WorkflowId)
            .ToArray();
        workflowEvents.Count(item => item.EventType is AuditEventType.ActionAttempt).ShouldBe(1);
        workflowEvents.ShouldNotContain(item => item.Outcome == AuditOutcome.Failed);
        workflowEvents
            .Where(item => item.EventType is AuditEventType.ActionExecution or AuditEventType.WorkflowCompleted)
            .ShouldAllBe(item => item.ReasonCodes.Contains(WorkflowAuditCodes.ExecutionUnavailable));
    }

    /// <summary>An executor call that does not return a result is explicitly audited as unknown.</summary>
    [Fact]
    public async Task RunAsync_ExecutorThrows_AuditsUnknownOutcomeAndRethrowsOriginalException()
    {
        var expected = new InvalidOperationException("downstream response was lost");
        var sequence = new List<string>();
        var harness = new WorkflowTestHarness();
        var auditSink = new SequencedAuditSink(harness.AuditSink, sequence);
        var executor = new RecordingActionExecutor(sequence) { ExceptionToThrow = expected };

        var actual = await Should.ThrowAsync<InvalidOperationException>(() =>
            harness.CreateOrchestrator(
                RepositoryReturning(WorkflowTestHarness.Evidence()),
                new StubRiskEvaluator(_ => WorkflowTestHarness.MediumEvaluation()),
                auditSink,
                actionExecutor: executor).RunAsync(
                    WorkflowTestHarness.Principal(),
                    WorkflowTestHarness.Command()));

        actual.ShouldBeSameAs(expected);
        sequence.ShouldBe(
        [
            "audit:ActionAttempt:AuthorizedForExecution",
            "execute",
            "audit:ActionExecution:ExecutionOutcomeUnknown",
            "audit:WorkflowCompleted:ExecutionOutcomeUnknown"
        ]);
        harness.AuditSink.Events
            .Where(item => item.EventType is AuditEventType.ActionExecution or AuditEventType.WorkflowCompleted)
            .ShouldAllBe(item => item.ReasonCodes.Contains(WorkflowAuditCodes.ExecutionOutcomeUnknown));
    }

    /// <summary>Verifies caller-visible audit identifiers are the persisted identifiers in write order.</summary>
    [Fact]
    public async Task RunAsync_ReturnedAuditIdsAndTimestampsMatchPersistedEvents()
    {
        var harness = new WorkflowTestHarness();

        var result = await harness.CreateOrchestrator().RunAsync(
            WorkflowTestHarness.Principal(),
            WorkflowTestHarness.Command());

        var events = harness.AuditSink.Events;
        events.Select(item => item.EventType).ShouldBe(
            [AuditEventType.ActionAttempt, AuditEventType.WorkflowCompleted]);
        events.Select(item => item.EventId).ShouldBe(result.AuditEventIds);
        events.ShouldAllBe(item => item.EventId != Guid.Empty);
        events.ShouldAllBe(item => item.TimestampUtc == WorkflowTestHarness.ExpectedUtcNow);
        events.ShouldAllBe(item => item.WorkflowId == result.WorkflowId);
    }

    /// <summary>Verifies operational dependency failures are safely audited and rethrown unchanged.</summary>
    [Theory]
    [InlineData("repository")]
    [InlineData("evaluator")]
    public async Task RunAsync_DependencyFailure_AuditsFailureAndRethrowsOriginalException(
        string failingDependency)
    {
        const string exceptionSecret = "EXCEPTION_SENTINEL_must-not-escape-in-audit";
        var expected = new InvalidOperationException(exceptionSecret);
        var repository = new StubEvidenceRepository((_, _) =>
            failingDependency == "repository"
                ? Task.FromException<EvidenceSearchResult>(expected)
                : Task.FromResult(WorkflowTestHarness.Evidence()));
        var evaluator = new StubRiskEvaluator(_ =>
            failingDependency == "evaluator"
                ? throw expected
                : WorkflowTestHarness.MediumEvaluation());
        var harness = new WorkflowTestHarness();

        var actual = await Should.ThrowAsync<InvalidOperationException>(() =>
            harness.CreateOrchestrator(repository, evaluator).RunAsync(
                WorkflowTestHarness.Principal(),
                WorkflowTestHarness.Command()));

        actual.ShouldBeSameAs(expected);
        var auditEvent = harness.AuditSink.Events.ShouldHaveSingleItem();
        auditEvent.EventType.ShouldBe(AuditEventType.WorkflowCompleted);
        auditEvent.Outcome.ShouldBe(AuditOutcome.Failed);
        JsonSerializer.Serialize(auditEvent).ShouldNotContain(exceptionSecret);
    }

    /// <summary>Verifies cancellation after workflow creation is audited before it propagates.</summary>
    [Fact]
    public async Task RunAsync_MidWorkflowCancellation_AuditsFailureAndPropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var repository = new StubEvidenceRepository((_, cancellationToken) =>
        {
            cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(WorkflowTestHarness.Evidence());
        });
        var harness = new WorkflowTestHarness();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            harness.CreateOrchestrator(evidenceRepository: repository).RunAsync(
                WorkflowTestHarness.Principal(),
                WorkflowTestHarness.Command(),
                cancellation.Token));

        var auditEvent = harness.AuditSink.Events.ShouldHaveSingleItem();
        auditEvent.EventType.ShouldBe(AuditEventType.WorkflowCompleted);
        auditEvent.Outcome.ShouldBe(AuditOutcome.Failed);
        harness.ActionExecutor.Executions.ShouldBeEmpty();
        harness.AuditSink.Events.ShouldNotContain(
            item => item.EventType == AuditEventType.ActionExecution);
    }

    /// <summary>Verifies mandatory audit persistence failure prevents a workflow result and side effect.</summary>
    [Fact]
    public async Task RunAsync_AuditSinkFailure_PropagatesWithoutReturningResult()
    {
        var expected = new InvalidOperationException("audit storage unavailable");
        var auditSink = new ThrowingAuditSink(expected);
        var harness = new WorkflowTestHarness();

        var actual = await Should.ThrowAsync<InvalidOperationException>(() =>
            harness.CreateOrchestrator(auditSink: auditSink).RunAsync(
                WorkflowTestHarness.Principal(),
                WorkflowTestHarness.Command(vendorId: "lakeshore-analytics")));

        actual.ShouldBeSameAs(expected);
        auditSink.CallCount.ShouldBe(2);
        harness.ActionExecutor.Executions.ShouldBeEmpty();
    }

    /// <summary>Accepted approval and authorization are audited before the side effect.</summary>
    [Fact]
    public async Task RunAsync_ValidApproval_AuditsExpectedExecutionOrder()
    {
        var harness = new WorkflowTestHarness();
        var approval = await harness.IssueApprovalAsync();
        var sequence = new List<string>();
        var auditSink = new SequencedAuditSink(harness.AuditSink, sequence);
        var executor = new RecordingActionExecutor(sequence);

        await harness.CreateOrchestrator(auditSink: auditSink, actionExecutor: executor).RunAsync(
            WorkflowTestHarness.Principal(),
            WorkflowTestHarness.Command(approvalId: approval.ApprovalId));

        sequence.ShouldBe(
        [
            "audit:ApprovalDecision:ApprovalAccepted",
            "audit:ActionAttempt:AuthorizedForExecution",
            "execute",
            "audit:ActionExecution:Executed",
            "audit:WorkflowCompleted:Executed"
        ]);
    }

    /// <summary>A supplied unknown approval is audited as rejected before the blocked attempt.</summary>
    [Fact]
    public async Task RunAsync_UnknownApproval_AuditsRejectionBeforeBlockedAttempt()
    {
        var harness = new WorkflowTestHarness();

        var result = await harness.CreateOrchestrator().RunAsync(
            WorkflowTestHarness.Principal(),
            WorkflowTestHarness.Command(approvalId: "unknown-approval"));

        result.ActionStatus.ShouldBe(ActionStatus.BlockedPendingApproval);
        harness.ActionExecutor.Executions.ShouldBeEmpty();
        harness.AuditSink.Events.Select(item => (item.EventType, item.Outcome)).ShouldBe(
        [
            (AuditEventType.ApprovalDecision, AuditOutcome.ApprovalRejected),
            (AuditEventType.ActionAttempt, AuditOutcome.BlockedPendingApproval),
            (AuditEventType.WorkflowCompleted, AuditOutcome.BlockedPendingApproval)
        ]);
    }

    private static StubEvidenceRepository RepositoryReturning(EvidenceSearchResult evidence) =>
        new((_, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(evidence);
        });

    private static void AssertNoAssessment(WorkflowRunResult result)
    {
        result.RiskLevel.ShouldBe(RiskLevel.Unknown);
        result.Recommendation.ShouldBeEmpty();
        result.Reasons.ShouldBeEmpty();
        result.Citations.ShouldBeEmpty();
        result.MissingEvidence.ShouldBeEmpty();
        result.RequiresApproval.ShouldBeFalse();
        result.AuditEventIds.Count.ShouldBe(2);
    }
}
