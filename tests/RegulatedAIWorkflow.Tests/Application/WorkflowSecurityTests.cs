using RegulatedAIWorkflow.Core.Contracts.Audit;
using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Tests.Application;

/// <summary>
/// Verifies validation, policy ambiguity, and citation failures remain closed.
/// Assignment-facing tenant, approval, audit, and hostile-prose cases live in the Required suites.
/// </summary>
public sealed class WorkflowSecurityTests
{
    /// <summary>Verifies invalid requests are audited without reaching evidence, policy, or execution.</summary>
    [Fact]
    public async Task RunAsync_InvalidRequests_RetrieveNothingAndReturnInvalidResult()
    {
        var repository = RepositoryReturning(WorkflowTestHarness.Evidence());
        var evaluator = new StubRiskEvaluator(_ => WorkflowTestHarness.MediumEvaluation());
        var harness = new WorkflowTestHarness();
        var orchestrator = harness.CreateOrchestrator(repository, evaluator);
        var invalidRequests = new (WorkflowPrincipal? Principal, WorkflowCommand? Command)[]
        {
            (null, WorkflowTestHarness.Command()),
            (WorkflowTestHarness.Principal(), null),
            (WorkflowTestHarness.Principal(tenantId: string.Empty), WorkflowTestHarness.Command()),
            (WorkflowTestHarness.Principal(userId: " user "), WorkflowTestHarness.Command()),
            (WorkflowTestHarness.Principal(userId: "user\u0000name"), WorkflowTestHarness.Command()),
            (WorkflowTestHarness.Principal(role: UserRole.Unknown), WorkflowTestHarness.Command()),
            (WorkflowTestHarness.Principal(role: (UserRole)999), WorkflowTestHarness.Command()),
            (WorkflowTestHarness.Principal(), WorkflowTestHarness.Command(vendorId: new string('v', 129))),
            (WorkflowTestHarness.Principal(), WorkflowTestHarness.Command(vendorId: " vendor ")),
            (WorkflowTestHarness.Principal(), WorkflowTestHarness.Command(question: new string('q', 2_001))),
            (WorkflowTestHarness.Principal(), WorkflowTestHarness.Command(question: "question\nsecret")),
            (WorkflowTestHarness.Principal(), WorkflowTestHarness.Command(action: WorkflowAction.Unknown)),
            (WorkflowTestHarness.Principal(), WorkflowTestHarness.Command(action: (WorkflowAction)999)),
            (WorkflowTestHarness.Principal(), WorkflowTestHarness.Command(approvalId: " approval ")),
            (WorkflowTestHarness.Principal(), WorkflowTestHarness.Command(approvalId: new string('a', 129)))
        };

        foreach (var request in invalidRequests)
        {
            var result = await orchestrator.RunAsync(request.Principal, request.Command);

            result.ActionStatus.ShouldBe(ActionStatus.BlockedInvalidRequest);
            AssertNoAssessment(result);
        }

        repository.CallCount.ShouldBe(0);
        evaluator.CallCount.ShouldBe(0);
        harness.ActionExecutor.Executions.ShouldBeEmpty();
        harness.AuditSink.Events.Count.ShouldBe(invalidRequests.Length * 2);
        harness.AuditSink.Events.ShouldAllBe(item => item.Outcome == AuditOutcome.InvalidRequest);
    }

    /// <summary>Verifies evaluator-declared ambiguity cannot become a pending-approval result.</summary>
    [Fact]
    public async Task RunAsync_AmbiguousEvaluation_WithholdsCitationsAndFailsClosed()
    {
        var harness = new WorkflowTestHarness();
        var repository = RepositoryReturning(WorkflowTestHarness.Evidence());
        var evaluator = new StubRiskEvaluator(_ =>
            WorkflowTestHarness.HighEvaluation(evidenceIsAmbiguous: true));

        var result = await harness.CreateOrchestrator(repository, evaluator).RunAsync(
            WorkflowTestHarness.Principal(),
            WorkflowTestHarness.Command());

        result.ActionStatus.ShouldBe(ActionStatus.BlockedEvidenceUnavailable);
        result.RiskLevel.ShouldBe(RiskLevel.High);
        result.Citations.ShouldBeEmpty();
        harness.ActionExecutor.Executions.ShouldBeEmpty();
    }

    /// <summary>Verifies invalid citation identity or provenance discards the assessment.</summary>
    [Theory]
    [InlineData("invented")]
    [InlineData("duplicate")]
    [InlineData("unsupported")]
    [InlineData("empty-id")]
    [InlineData("empty-snippet")]
    public async Task RunAsync_InvalidCitation_DiscardsAssessmentAndFailsClosed(string scenario)
    {
        var setup = CitationScenario(scenario);
        var harness = new WorkflowTestHarness();

        var result = await harness.CreateOrchestrator(
            RepositoryReturning(setup.Evidence),
            new StubRiskEvaluator(_ => setup.Evaluation)).RunAsync(
                WorkflowTestHarness.Principal(),
                WorkflowTestHarness.Command());

        result.ActionStatus.ShouldBe(ActionStatus.BlockedEvidenceUnavailable);
        AssertNoAssessment(result);
        harness.ActionExecutor.Executions.ShouldBeEmpty();
        harness.AuditSink.Events.ShouldAllBe(item =>
            item.ReasonCodes.Count == 1 &&
            item.ReasonCodes[0] == WorkflowAuditCodes.CitationVerificationFailed);
        harness.AuditSink.Events.ShouldAllBe(item => item.RiskLevel == null);
        harness.AuditSink.Events.ShouldAllBe(item => item.PolicyVersion == null);
        harness.AuditSink.Events.ShouldAllBe(item => item.ReferencedDocumentIds.Count == 0);
        harness.AuditSink.Events.ShouldAllBe(item => item.MissingEvidenceCodes.Count == 0);
    }

    private static StubEvidenceRepository RepositoryReturning(EvidenceSearchResult evidence) =>
        new((_, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(evidence);
        });

    private static (EvidenceSearchResult Evidence, RiskEvaluation Evaluation) CitationScenario(string scenario)
    {
        var evidence = WorkflowTestHarness.Evidence();
        return scenario switch
        {
            "invented" => (evidence, WorkflowTestHarness.HighEvaluation(
                [new RiskCitationReference("invented-document")])),
            "duplicate" => (evidence, WorkflowTestHarness.HighEvaluation(
                [new RiskCitationReference("policy-document"), new RiskCitationReference("policy-document")])),
            "unsupported" => (
                new EvidenceSearchResult(
                    [
                        WorkflowTestHarness.Document("policy-document", "supported"),
                        WorkflowTestHarness.Document("unlinked-document", "unlinked")
                    ],
                    [WorkflowTestHarness.Fact("policy-document", EvidenceFactType.SecurityEvidenceRequired)]),
                WorkflowTestHarness.HighEvaluation([new RiskCitationReference("unlinked-document")])),
            "empty-id" => (evidence, WorkflowTestHarness.HighEvaluation(
                [new RiskCitationReference(string.Empty)])),
            _ => (WorkflowTestHarness.Evidence(string.Empty), WorkflowTestHarness.HighEvaluation(
                [new RiskCitationReference("policy-document")]))
        };
    }

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
