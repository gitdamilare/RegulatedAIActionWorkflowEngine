using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Tests;

/// <summary>
/// markVendorApproved is high risk by policy, so approval is always required. What evidence changes is
/// the reasons, the named gaps, and the citations. These assert the evaluator is live, not decorative.
/// </summary>
public sealed class RiskEvaluatorTests
{
    private const string Baseline = "ACTION_MARK_VENDOR_APPROVED_HIGH_RISK";
    private const string PaymentScope = "PAYMENT_DATA_IN_SCOPE";
    private const string SensitiveScope = "SENSITIVE_DATA_IN_SCOPE";

    /// <summary>
    /// Both tenants are in regulated scope, so both report the scope reasons. Only the tenant with
    /// incomplete evidence reports gaps, and a gap reason always arrives with a named missing item.
    /// </summary>
    [Theory]
    [InlineData(
        Harness.TenantA,
        new[] { Baseline, PaymentScope, SensitiveScope, "SOC2_MISSING", "RETENTION_SCHEDULE_MISSING", "BREACH_NOTIFICATION_MISSING" },
        new[] { "SOC2_REPORT", "DATA_RETENTION_SCHEDULE", "BREACH_NOTIFICATION_CLAUSE" })]
    [InlineData(
        Harness.TenantB,
        new[] { Baseline, PaymentScope, SensitiveScope },
        new string[0])]
    public async Task RunAsync_EvidenceCompleteness_DrivesReasonsGapsAndCitations(
        string tenantId,
        string[] expectedReasonCodes,
        string[] expectedGapCodes)
    {
        var harness = new Harness();

        var result = await harness.Orchestrator().RunAsync(
            Harness.Principal(tenantId),
            Harness.Command());

        // The action baseline holds the level at high in both cases.
        result.RiskLevel.ShouldBe(RiskLevel.High);
        result.RequiresApproval.ShouldBeTrue();
        result.ActionStatus.ShouldBe(ActionStatus.BlockedPendingApproval);

        // The evidence decides everything else.
        result.Reasons.Select(reason => reason.Code).ShouldBe(expectedReasonCodes);
        result.MissingEvidence.Select(gap => gap.Code).ShouldBe(expectedGapCodes);
        result.Citations.ShouldNotBeEmpty();
    }

    /// <summary>Citations name only documents that actually supplied a fact the policy read.</summary>
    [Fact]
    public async Task RunAsync_Citations_NameOnlyRetrievedDocumentsThatSuppliedAFact()
    {
        var harness = new Harness();
        var documents = await harness.Evidence.SearchEvidenceAsync(
            new EvidenceQuery(Harness.TenantA, Harness.Vendor),
            CancellationToken.None);

        var result = await harness.Orchestrator().RunAsync(Harness.Principal(), Harness.Command());

        var retrievedIds = documents.Select(document => document.DocumentId).ToArray();
        result.Citations.ShouldAllBe(citation => retrievedIds.Contains(citation.DocumentId));
        result.Citations.Select(citation => citation.DocumentId).ShouldBeUnique();

        var citedDocuments = documents.Where(document =>
            result.Citations.Any(citation => citation.DocumentId == document.DocumentId));
        citedDocuments.ShouldAllBe(document => document.FactTypes.Count > 0);
    }
}
