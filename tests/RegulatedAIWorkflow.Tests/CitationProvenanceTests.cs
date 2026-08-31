using RegulatedAIWorkflow.Core.Contracts.Audit;
using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Tests;

/// <summary>
/// A citation is a claim that the assessment rests on evidence the system actually holds. When that claim
/// cannot be checked the run stops, rather than returning a result whose citation list was quietly
/// shortened. Same principle as the scope re-assertion: a component that disagrees with the evidence is a
/// bug, not a branch.
/// </summary>
public sealed class CitationProvenanceTests
{
    private static RiskEvaluation Assessment(params string[] citedDocumentIds) =>
        new(
            RiskLevel.High,
            "Do not approve yet.",
            [new RiskReason("TEST_REASON", "A reason.")],
            [],
            citedDocumentIds,
            RequiresApproval: false);

    [Fact]
    public async Task RunAsync_AssessmentCitesADocumentThatWasNotRetrieved_BlocksAndNeverCallsExecutor()
    {
        var harness = new Harness();

        var result = await harness
            .Orchestrator(riskEvaluator: new StubRiskEvaluator(Assessment("document-that-was-never-retrieved")))
            .RunAsync(Harness.Principal(), Harness.Command());

        result.ActionStatus.ShouldBe(ActionStatus.BlockedEvidenceUnavailable);
        result.Citations.ShouldBeEmpty();
        harness.Executor.CallCount.ShouldBe(0);
        harness.Audit.Events.ShouldAllBe(auditEvent =>
            auditEvent.ReasonCodes.Contains(WorkflowAuditCodes.CitationVerificationFailed));
    }

    /// <summary>
    /// The failure is all or nothing. One unverifiable citation among several real ones still stops the
    /// run, because returning the resolvable subset would hide the disagreement it was meant to surface.
    /// </summary>
    [Fact]
    public async Task RunAsync_OneUnverifiableCitationAmongValidOnes_StillBlocks()
    {
        var harness = new Harness();

        var result = await harness
            .Orchestrator(riskEvaluator: new StubRiskEvaluator(
                Assessment("northstar-silverline-submission", "invented-document")))
            .RunAsync(Harness.Principal(), Harness.Command());

        result.ActionStatus.ShouldBe(ActionStatus.BlockedEvidenceUnavailable);
        harness.Executor.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_EveryCitationResolves_ProducesSnippetsForTheRetainedDocuments()
    {
        var harness = new Harness();

        var result = await harness
            .Orchestrator(riskEvaluator: new StubRiskEvaluator(Assessment("northstar-silverline-submission")))
            .RunAsync(Harness.Principal(), Harness.Command());

        result.ActionStatus.ShouldBe(ActionStatus.Executed);
        var citation = result.Citations.ShouldHaveSingleItem();
        citation.DocumentId.ShouldBe("northstar-silverline-submission");
        citation.Snippet.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>The real evaluator cites only documents it drew facts from, so the guard never fires in normal use.</summary>
    [Theory]
    [InlineData(Harness.TenantA, Harness.Vendor)]
    [InlineData(Harness.TenantB, Harness.Vendor)]
    [InlineData(Harness.TenantA, Harness.LowRiskVendor)]
    public async Task RunAsync_RealEvaluator_AlwaysCitesRetainedEvidence(string tenantId, string vendorId)
    {
        var harness = new Harness();

        var result = await harness.Orchestrator().RunAsync(
            Harness.Principal(tenantId: tenantId),
            Harness.Command(vendorId: vendorId, action: WorkflowAction.RequestVendorEvidence));

        result.ActionStatus.ShouldNotBe(ActionStatus.BlockedEvidenceUnavailable);
    }
}
