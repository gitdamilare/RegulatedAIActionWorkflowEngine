using RegulatedAIWorkflow.Core.Contracts.Audit;
using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Risk;
using RegulatedAIWorkflow.Infrastructure.Evidence;
using RegulatedAIWorkflow.Tests.Application;

namespace RegulatedAIWorkflow.Tests.Required;

/// <summary>
/// Required test 1: tenant-scoped retrieval and Core revalidation prevent evidence leakage.
/// The strongest assertions prove that foreign data never reaches policy or execution and that
/// callers cannot distinguish a subject that exists only in another tenant from one that does not exist.
/// </summary>
public sealed class Required_1_TenantIsolationTests
{
    /// <summary>Verifies the same vendor identifier does not merge tenant corpora.</summary>
    [Fact]
    public async Task SearchEvidenceAsync_SharedVendorAcrossTenants_KeepsCorporaIsolated()
    {
        var repository = new InMemoryEvidenceRepository();
        var northstar = await SearchAsync(repository, "northstar-bank", "silverline-payments");
        var harborview = await SearchAsync(repository, "harborview-bank", "silverline-payments");

        northstar.Documents.Select(document => document.DocumentId)
            .Intersect(harborview.Documents.Select(document => document.DocumentId), StringComparer.Ordinal)
            .ShouldBeEmpty();
        northstar.Facts.ShouldAllBe(fact => fact.TenantId == "northstar-bank");
        harborview.Facts.ShouldAllBe(fact => fact.TenantId == "harborview-bank");
    }

    /// <summary>Verifies foreign, orphaned, and duplicate evidence stops before policy or execution.</summary>
    [Theory]
    [InlineData("foreign")]
    [InlineData("orphan")]
    [InlineData("duplicate")]
    public async Task RunAsync_ScopeViolation_FailsBeforeRiskEvaluation(string scenario)
    {
        var repository = RepositoryReturning(ScopeViolationEvidence(scenario));
        var evaluator = new StubRiskEvaluator(_ => WorkflowTestHarness.MediumEvaluation());
        var harness = new WorkflowTestHarness();

        var result = await harness.CreateOrchestrator(repository, evaluator).RunAsync(
            WorkflowTestHarness.Principal(),
            WorkflowTestHarness.Command());

        repository.CallCount.ShouldBe(1);
        evaluator.CallCount.ShouldBe(0);
        harness.ActionExecutor.Executions.ShouldBeEmpty();
        result.ActionStatus.ShouldBe(ActionStatus.BlockedEvidenceUnavailable);
        AssertNoAssessment(result);
        harness.AuditSink.Events.ShouldAllBe(item =>
            item.ReasonCodes.Contains(WorkflowAuditCodes.EvidenceScopeViolation, StringComparer.Ordinal));
    }

    /// <summary>Verifies an unknown tenant-scoped subject is denied without assessment or execution.</summary>
    [Fact]
    public async Task RunAsync_EmptyEvidence_ReturnsUnknownSubjectDenial()
    {
        var harness = new WorkflowTestHarness();
        var repository = RepositoryReturning(new EvidenceSearchResult([], []));
        var evaluator = new StubRiskEvaluator(_ => WorkflowTestHarness.HighEvaluation());

        var result = await harness.CreateOrchestrator(repository, evaluator).RunAsync(
            WorkflowTestHarness.Principal(),
            WorkflowTestHarness.Command());

        repository.CallCount.ShouldBe(1);
        evaluator.CallCount.ShouldBe(0);
        harness.ActionExecutor.Executions.ShouldBeEmpty();
        result.ActionStatus.ShouldBe(ActionStatus.DeniedUnknownSubject);
        result.RiskLevel.ShouldBe(RiskLevel.Unknown);
        result.Recommendation.ShouldBe("No such subject in this tenant.");
        result.Reasons.ShouldBe(
            [new RiskReason(WorkflowAuditCodes.UnknownSubject, "No such subject in this tenant.")]);
        result.Citations.ShouldBeEmpty();
        result.MissingEvidence.ShouldBeEmpty();
        result.RequiresApproval.ShouldBeFalse();
        result.AuditEventIds.Count.ShouldBe(2);
        harness.AuditSink.Events.Select(item => item.EventType).ShouldBe(
            [AuditEventType.ActionAttempt, AuditEventType.WorkflowCompleted]);
        harness.AuditSink.Events.ShouldAllBe(item =>
            item.Outcome == AuditOutcome.DeniedUnknownSubject);
        harness.AuditSink.Events.ShouldAllBe(item => item.RiskLevel == null);
        harness.AuditSink.Events.ShouldAllBe(item => item.PolicyVersion == null);
        harness.AuditSink.Events.ShouldAllBe(item => item.ReferencedDocumentIds.Count == 0);
        harness.AuditSink.Events.ShouldAllBe(item =>
            item.ReasonCodes.Count == 1 &&
            item.ReasonCodes[0] == WorkflowAuditCodes.UnknownSubject);
        harness.AuditSink.Events.ShouldAllBe(item => item.MissingEvidenceCodes.Count == 0);
    }

    /// <summary>Verifies tenant-scoped absence cannot become a cross-tenant existence oracle.</summary>
    [Fact]
    public async Task RunAsync_ForeignOnlyAndUnknownSubjects_ReturnIndistinguishableDenials()
    {
        var harness = new WorkflowTestHarness();
        var orchestrator = harness.CreateOrchestrator();
        var harborviewPrincipal = WorkflowTestHarness.Principal(tenantId: "harborview-bank");

        var foreignOnly = await orchestrator.RunAsync(
            harborviewPrincipal,
            WorkflowTestHarness.Command(vendorId: "lakeshore-analytics"));
        var unknown = await orchestrator.RunAsync(
            harborviewPrincipal,
            WorkflowTestHarness.Command(vendorId: "vendor-does-not-exist"));

        foreignOnly.ActionStatus.ShouldBe(ActionStatus.DeniedUnknownSubject);
        foreignOnly.ActionStatus.ShouldBe(unknown.ActionStatus);
        foreignOnly.RiskLevel.ShouldBe(unknown.RiskLevel);
        foreignOnly.Recommendation.ShouldBe(unknown.Recommendation);
        foreignOnly.Reasons.ShouldBe(unknown.Reasons);
        foreignOnly.Citations.ShouldBe(unknown.Citations);
        foreignOnly.MissingEvidence.ShouldBe(unknown.MissingEvidence);
        foreignOnly.RequiresApproval.ShouldBe(unknown.RequiresApproval);
        harness.ActionExecutor.Executions.ShouldBeEmpty();
    }

    private static Task<EvidenceSearchResult> SearchAsync(
        InMemoryEvidenceRepository repository,
        string tenantId,
        string vendorId) =>
        repository.SearchEvidenceAsync(new EvidenceQuery(tenantId, vendorId), CancellationToken.None);

    private static StubEvidenceRepository RepositoryReturning(EvidenceSearchResult evidence) =>
        new((_, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(evidence);
        });

    private static EvidenceSearchResult ScopeViolationEvidence(string scenario)
    {
        var retained = WorkflowTestHarness.Document("retained-document", "retained");
        return scenario switch
        {
            "foreign" => new EvidenceSearchResult(
                [retained, WorkflowTestHarness.Document("foreign-document", "foreign", tenantId: "foreign-tenant")],
                [WorkflowTestHarness.Fact("retained-document", EvidenceFactType.ContainsSensitiveData)]),
            "orphan" => new EvidenceSearchResult(
                [retained],
                [WorkflowTestHarness.Fact("missing-document", EvidenceFactType.ContainsSensitiveData)]),
            _ => new EvidenceSearchResult(
                [retained, retained with { DocumentType = EvidenceDocumentType.Contract }],
                [WorkflowTestHarness.Fact("retained-document", EvidenceFactType.ContainsSensitiveData)])
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
