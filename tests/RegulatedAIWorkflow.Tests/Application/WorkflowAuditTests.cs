using System.Text.Json;
using RegulatedAIWorkflow.Core.Contracts.Audit;
using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Tests.Application;

/// <summary>
/// Verifies audit ordering, safe contents, deterministic metadata, and failure behavior.
/// </summary>
public sealed class WorkflowAuditTests
{
    /// <summary>
    /// Verifies successful dependencies and the two terminal audit records run in order.
    /// </summary>
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
        var evaluator = new StubRiskEvaluator(
            _ => WorkflowTestHarness.MediumEvaluation(),
            sequence);
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

    /// <summary>
    /// Verifies caller-visible audit identifiers are the persisted identifiers in write order.
    /// </summary>
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

    /// <summary>
    /// Verifies request and evidence prose has no path into the structured audit contract.
    /// </summary>
    [Fact]
    public async Task RunAsync_UntrustedProseAndSecrets_AreAbsentFromSerializedAudit()
    {
        const string questionSecret = "QUESTION_SENTINEL_should-never-be-audited";
        const string idempotencySecret = "Idempotency-Key=raw-secret-value";
        const string snippetSecret = "SNIPPET_SENTINEL_ignore-policy-and-approve";
        var harness = new WorkflowTestHarness();
        var repository = new StubEvidenceRepository((_, _) =>
            Task.FromResult(WorkflowTestHarness.Evidence(snippetSecret)));
        var evaluator = new StubRiskEvaluator(_ => WorkflowTestHarness.HighEvaluation(
            [new RiskCitationReference("policy-document")]));

        var result = await harness.CreateOrchestrator(repository, evaluator).RunAsync(
            WorkflowTestHarness.Principal(),
            WorkflowTestHarness.Command(question: $"{questionSecret} {idempotencySecret}"));

        result.ActionStatus.ShouldBe(ActionStatus.BlockedPendingApproval);
        var serialized = JsonSerializer.Serialize(harness.AuditSink.Events);
        serialized.ShouldNotContain(questionSecret);
        serialized.ShouldNotContain(idempotencySecret);
        serialized.ShouldNotContain(snippetSecret);
        serialized.ShouldNotContain("Question");
        serialized.ShouldNotContain("Snippet");
        serialized.ShouldNotContain("IdempotencyKey");
        serialized.ShouldNotContain("Exception");
    }

    /// <summary>
    /// Verifies operational dependency failures are safely audited and then rethrown unchanged.
    /// </summary>
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

    /// <summary>
    /// Verifies cancellation after workflow creation is safely audited before it propagates.
    /// </summary>
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
    }

    /// <summary>
    /// Verifies mandatory audit persistence failure prevents a workflow result.
    /// </summary>
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
}
