using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Infrastructure.Evidence;

namespace RegulatedAIWorkflow.Tests.Infrastructure.Evidence;

/// <summary>
/// Verifies tenant and vendor isolation in the in-memory evidence adapter.
/// </summary>
public sealed class InMemoryEvidenceRepositoryTests
{
    private readonly InMemoryEvidenceRepository repository = new();

    /// <summary>
    /// Verifies Northstar's Silverline corpus and normalized facts.
    /// </summary>
    [Fact]
    public async Task SearchEvidenceAsync_NorthstarSilverline_ReturnsExpectedEvidence()
    {
        var result = await SearchAsync("northstar-bank", "silverline-payments");

        result.Documents.Select(document => document.DocumentId).ShouldBe(
        [
            "northstar-policy-002",
            "northstar-silverline-contract",
            "northstar-silverline-submission"
        ]);
        result.Facts.Select(fact => fact.FactType).ShouldBe(
        [
            EvidenceFactType.SecurityEvidenceRequired,
            EvidenceFactType.BreachNotificationMissing,
            EvidenceFactType.ProcessesPaymentData,
            EvidenceFactType.ContainsSensitiveData
        ]);
        result.Documents.ShouldAllBe(document =>
            document.TenantId == "northstar-bank" && document.VendorId == "silverline-payments");
        result.Facts.ShouldAllBe(fact =>
            fact.TenantId == "northstar-bank" && fact.VendorId == "silverline-payments");
    }

    /// <summary>
    /// Verifies Northstar's second vendor is independently addressable.
    /// </summary>
    [Fact]
    public async Task SearchEvidenceAsync_NorthstarLakeshore_ReturnsOnlyLakeshoreEvidence()
    {
        var result = await SearchAsync("northstar-bank", "lakeshore-analytics");

        result.Documents.Select(document => document.DocumentId).ShouldBe(
            ["northstar-lakeshore-contract"]);
        result.Facts.Select(fact => fact.FactType).ShouldBe(
        [
            EvidenceFactType.ContainsSensitiveData,
            EvidenceFactType.BreachNotificationPresent
        ]);
        result.Documents.ShouldAllBe(document => document.VendorId == "lakeshore-analytics");
        result.Facts.ShouldAllBe(fact => fact.VendorId == "lakeshore-analytics");
    }

    /// <summary>
    /// Verifies Harborview's Silverline corpus remains distinct from Northstar's corpus.
    /// </summary>
    [Fact]
    public async Task SearchEvidenceAsync_HarborviewSilverline_ReturnsExpectedEvidence()
    {
        var result = await SearchAsync("harborview-bank", "silverline-payments");

        result.Documents.Select(document => document.DocumentId).ShouldBe(
        [
            "harborview-policy-001",
            "harborview-silverline-contract",
            "harborview-silverline-soc2",
            "harborview-silverline-retention"
        ]);
        result.Facts.Select(fact => fact.FactType).ShouldBe(
        [
            EvidenceFactType.SecurityEvidenceRequired,
            EvidenceFactType.BreachNotificationPresent,
            EvidenceFactType.ProcessesPaymentData,
            EvidenceFactType.ContainsSensitiveData,
            EvidenceFactType.Soc2Available,
            EvidenceFactType.DataRetentionScheduleAvailable
        ]);
        result.Documents.ShouldAllBe(document =>
            document.TenantId == "harborview-bank" && document.VendorId == "silverline-payments");
        result.Facts.ShouldAllBe(fact =>
            fact.TenantId == "harborview-bank" && fact.VendorId == "silverline-payments");
    }

    /// <summary>
    /// Verifies identifiers are matched exactly and both scope components are required.
    /// </summary>
    [Theory]
    [InlineData("Northstar-bank", "silverline-payments")]
    [InlineData("northstar-bank", "Silverline-payments")]
    [InlineData("northstar-bank", "unknown-vendor")]
    [InlineData("harborview-bank", "lakeshore-analytics")]
    public async Task SearchEvidenceAsync_NonMatchingOrdinalScope_ReturnsEmptyEvidence(
        string tenantId,
        string vendorId)
    {
        var result = await SearchAsync(tenantId, vendorId);

        result.Documents.ShouldBeEmpty();
        result.Facts.ShouldBeEmpty();
    }

    private Task<EvidenceSearchResult> SearchAsync(string tenantId, string vendorId) =>
        repository.SearchEvidenceAsync(
            new EvidenceQuery(tenantId, vendorId),
            CancellationToken.None);
}
