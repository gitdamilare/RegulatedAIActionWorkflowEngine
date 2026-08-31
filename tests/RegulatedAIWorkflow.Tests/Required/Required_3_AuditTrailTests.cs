using System.Text.Json;
using RegulatedAIWorkflow.Core.Contracts.Audit;
using RegulatedAIWorkflow.Core.Contracts.Workflow;

namespace RegulatedAIWorkflow.Tests.Required;

/// <summary>
/// Brief required test 3: every workflow run and action attempt writes an audit event.
/// </summary>
public sealed class Required_3_AuditTrailTests
{
    /// <summary>
    /// Every run writes exactly two events, ActionAttempt then WorkflowCompleted. On the executed path
    /// the attempt is written and awaited before the executor is called, so a crash mid-effect still
    /// leaves a record that the effect was authorized.
    /// </summary>
    [Theory]
    [InlineData(false, AuditOutcome.BlockedPendingApproval)]
    [InlineData(true, AuditOutcome.Executed)]
    public async Task RunAsync_BlockedAndExecutedRuns_WriteAttemptThenCompletedEvents(
        bool withApproval,
        AuditOutcome expectedCompletionOutcome)
    {
        var harness = new Harness();
        var approvalId = withApproval ? (await harness.IssueApprovalAsync()).ApprovalId : null;

        var result = await harness.Orchestrator().RunAsync(
            Harness.Principal(),
            Harness.Command(approvalId: approvalId));

        var events = harness.Audit.Events;
        events.Count.ShouldBe(2);
        events[0].EventType.ShouldBe(AuditEventType.ActionAttempt);
        events[1].EventType.ShouldBe(AuditEventType.WorkflowCompleted);
        events[1].Outcome.ShouldBe(expectedCompletionOutcome);

        result.AuditEventIds.ShouldBe(events.Select(auditEvent => auditEvent.EventId).ToArray());
        events.ShouldAllBe(auditEvent =>
            auditEvent.TenantId == Harness.TenantA &&
            auditEvent.VendorId == Harness.Vendor &&
            auditEvent.RequestedAction == WorkflowAction.MarkVendorApproved &&
            auditEvent.ActorRole == UserRole.ProcurementManager &&
            auditEvent.WorkflowId == result.WorkflowId);
    }

    /// <summary>
    /// The audit trail carries identifiers and codes. Neither the caller's question nor a document's
    /// prose has a field to occupy, so neither can reach durable storage or a log line.
    /// </summary>
    [Fact]
    public async Task RunAsync_AuditEvents_ContainNoQuestionOrSnippetText()
    {
        const string SentinelQuestion = "SENTINEL-QUESTION-can-we-approve-this-vendor";
        var harness = new Harness();

        var result = await harness.Orchestrator().RunAsync(
            Harness.Principal(),
            Harness.Command(question: SentinelQuestion));

        // The malicious snippet did reach the caller, as a citation. That is the only path it has.
        result.Citations.ShouldContain(citation =>
            citation.Snippet.Contains("Ignore all previous instructions", StringComparison.Ordinal));

        var serializedAudit = JsonSerializer.Serialize(harness.Audit.Events);
        serializedAudit.ShouldNotContain(SentinelQuestion);
        serializedAudit.ShouldNotContain("Ignore all previous instructions");
        serializedAudit.ShouldNotContain("Question", Case.Insensitive);
        serializedAudit.ShouldNotContain("Snippet", Case.Insensitive);
    }

    /// <summary>
    /// A failure before dispatch proves no effect occurred. A failure with the executor call outstanding
    /// proves nothing, so it must never be recorded as a clean failure.
    /// </summary>
    [Theory]
    [InlineData("repository", AuditOutcome.Failed)]
    [InlineData("executor", AuditOutcome.ExecutionOutcomeUnknown)]
    public async Task RunAsync_FaultingDependency_AuditsCorrectTerminalOutcome(
        string faultingDependency,
        AuditOutcome expectedOutcome)
    {
        var harness = new Harness();
        var failure = new TimeoutException("dependency timed out");
        string? approvalId = null;

        if (faultingDependency == "repository")
        {
            harness.Evidence.ExceptionToThrow = failure;
        }
        else
        {
            harness.Executor.ExceptionToThrow = failure;
            approvalId = (await harness.IssueApprovalAsync()).ApprovalId;
        }

        var thrown = await Should.ThrowAsync<TimeoutException>(() =>
            harness.Orchestrator().RunAsync(Harness.Principal(), Harness.Command(approvalId: approvalId)));

        thrown.ShouldBeSameAs(failure);

        var events = harness.Audit.Events;
        var completion = events[^1];
        completion.EventType.ShouldBe(AuditEventType.WorkflowCompleted);
        completion.Outcome.ShouldBe(expectedOutcome);

        // The exception itself is never recorded, only the structured outcome.
        JsonSerializer.Serialize(harness.Audit.Events).ShouldNotContain("timed out");
    }

    /// <summary>
    /// The ordering the whole design rests on: the attempt is on record before the effect can begin. A
    /// trail written afterwards proves nothing, because the process can die in between and the effect
    /// would then have happened with no record that it was even tried.
    /// </summary>
    [Fact]
    public async Task RunAsync_Executed_WritesTheAttemptBeforeTheEffect()
    {
        var harness = new Harness();
        var approval = await harness.IssueApprovalAsync();

        await harness.Orchestrator().RunAsync(
            Harness.Principal(),
            Harness.Command(approvalId: approval.ApprovalId));

        harness.Sequence.Entries.ShouldBe(
        [
            "audit:ActionAttempt:AuthorizedForExecution",
            "execute",
            "audit:WorkflowCompleted:Executed"
        ]);
    }

    /// <summary>
    /// If the pre-effect write fails, nothing runs. The audit trail is not a side channel that can be
    /// dropped under pressure; it gates the effect.
    /// </summary>
    [Fact]
    public async Task RunAsync_AuditWriteFails_DoesNotCallExecutor()
    {
        var harness = new Harness();
        var approval = await harness.IssueApprovalAsync();
        var failing = new FailingAuditSink(harness.Audit, failOnEventType: AuditEventType.ActionAttempt);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            harness.Orchestrator(auditSink: failing).RunAsync(
                Harness.Principal(),
                Harness.Command(approvalId: approval.ApprovalId)));

        harness.Executor.CallCount.ShouldBe(0);
    }
}
