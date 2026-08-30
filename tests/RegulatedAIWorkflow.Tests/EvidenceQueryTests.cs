using RegulatedAIWorkflow.Core.Domain.Evidence;

namespace RegulatedAIWorkflow.Tests;

/// <summary>
/// Scope is one value with one definition of membership, used by the adapter that answers the query and
/// by the Core assertion that distrusts the answer. These prove that definition directly.
/// </summary>
public sealed class EvidenceQueryTests
{
    [Theory]
    [InlineData(Harness.TenantA, Harness.Vendor, true)]
    [InlineData(Harness.TenantB, Harness.Vendor, false)]
    [InlineData(Harness.TenantA, Harness.TenantAOnlyVendor, false)]
    public void Covers_DocumentFromAnotherTenantOrVendor_IsOutOfScope(
        string tenantId,
        string vendorId,
        bool expected)
    {
        var query = new EvidenceQuery(tenantId, vendorId);

        var document = new EvidenceDocument(
            "doc-1",
            Harness.TenantA,
            Harness.Vendor,
            EvidenceDocumentType.Contract,
            [EvidenceFactType.ProcessesPaymentData],
            UntrustedText.FromExternalSource("vendor prose"));

        query.Covers(document).ShouldBe(expected);
    }

    /// <summary>An unscoped query is not representable, so no caller can accidentally ask for everything.</summary>
    [Theory]
    [InlineData("", Harness.Vendor)]
    [InlineData("   ", Harness.Vendor)]
    [InlineData(Harness.TenantA, "")]
    public void Constructor_BlankScope_IsRejected(string tenantId, string vendorId) =>
        Should.Throw<ArgumentException>(() => new EvidenceQuery(tenantId, vendorId));
}
