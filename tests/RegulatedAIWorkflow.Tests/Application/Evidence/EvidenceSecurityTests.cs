using RegulatedAIWorkflow.Core.Application;
using RegulatedAIWorkflow.Core.Domain.Evidence;

namespace RegulatedAIWorkflow.Tests.Application.Evidence;

/// <summary>
/// Verifies Core rejects evidence that violates tenant, vendor, and provenance boundaries.
/// </summary>
public sealed class EvidenceSecurityTests
{
    /// <summary>
    /// Verifies valid evidence passes the trust-boundary check unchanged.
    /// </summary>
    [Fact]
    public void EnforceScope_CorrectlyScopedEvidence_RetainsAllContentWithoutViolation()
    {
        var documentId = "northstar-policy-002";
        var tenantId = "northstar-bank";
        var vendorId = "silverline-payments";
        var document = CreateDocument(documentId, tenantId, vendorId);
        var fact = CreateSecurityEvidenceRequiredFact(documentId, tenantId, vendorId);
        var retrieved = new EvidenceSearchResult([document], [fact]);

        var scoped = EvidenceSecurity.EnforceScope(
            retrieved,
            tenantId,
            vendorId);

        scoped.HadOutOfScopeContent.ShouldBeFalse();
        scoped.Evidence.Documents.ShouldBe([document]);
        scoped.Evidence.Facts.ShouldBe([fact]);
    }

    /// <summary>
    /// Verifies Core independently removes both cross-tenant and cross-vendor leakage.
    /// </summary>
    [Fact]
    public void EnforceScope_LeakyRepositoryOutput_RemovesForeignContentAndReportsViolation()
    {
        var retainedDocument = CreateDocument(
            "northstar-policy-002",
            "northstar-bank",
            "silverline-payments");
        var crossTenantDocument = CreateDocument(
            "harborview-policy-001",
            "harborview-bank",
            "silverline-payments");
        var crossVendorDocument = CreateDocument(
            "northstar-lakeshore-contract",
            "northstar-bank",
            "lakeshore-analytics");
        var retainedFact = CreateSecurityEvidenceRequiredFact(
            retainedDocument.DocumentId,
            retainedDocument.TenantId,
            retainedDocument.VendorId);
        var retrieved = new EvidenceSearchResult(
            [retainedDocument, crossTenantDocument, crossVendorDocument],
            [
                retainedFact,
                CreateSecurityEvidenceRequiredFact(crossTenantDocument.DocumentId, crossTenantDocument.TenantId, crossTenantDocument.VendorId),
                CreateSecurityEvidenceRequiredFact(crossVendorDocument.DocumentId, crossVendorDocument.TenantId, crossVendorDocument.VendorId)
            ]);

        var scoped = EvidenceSecurity.EnforceScope(
            retrieved,
            "northstar-bank",
            "silverline-payments");

        scoped.HadOutOfScopeContent.ShouldBeTrue();
        scoped.Evidence.Documents.ShouldBe([retainedDocument]);
        scoped.Evidence.Facts.ShouldBe([retainedFact]);
        scoped.Evidence.Documents.ShouldNotContain(document => document.TenantId == "harborview-bank");
        scoped.Evidence.Facts.ShouldNotContain(fact => fact.TenantId == "harborview-bank");
    }

    /// <summary>
    /// Verifies a fact cannot survive without its retained source document.
    /// </summary>
    [Fact]
    public void EnforceScope_OrphanFact_RemovesFactAndReportsViolation()
    {
        var document = CreateDocument("northstar-policy-002", "northstar-bank", "silverline-payments");
        var orphan = CreateSecurityEvidenceRequiredFact("missing-document", "northstar-bank", "silverline-payments");

        var scoped = EvidenceSecurity.EnforceScope(
            new EvidenceSearchResult([document], [orphan]),
            "northstar-bank",
            "silverline-payments");

        scoped.HadOutOfScopeContent.ShouldBeTrue();
        scoped.Evidence.Documents.ShouldBe([document]);
        scoped.Evidence.Facts.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies ambiguous document identity is reported even when both documents match scope.
    /// </summary>
    [Fact]
    public void EnforceScope_DuplicateDocumentIds_ReportsViolation()
    {
        var first = CreateDocument("duplicate-document", "northstar-bank", "silverline-payments");
        var second = first with { DocumentType = EvidenceDocumentType.Contract };

        var scoped = EvidenceSecurity.EnforceScope(
            new EvidenceSearchResult([first, second], []),
            "northstar-bank",
            "silverline-payments");

        scoped.HadOutOfScopeContent.ShouldBeTrue();
        scoped.Evidence.Documents.ShouldBe([first, second]);
        scoped.Evidence.Facts.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies legitimate absence is distinguishable from inconsistent repository output.
    /// </summary>
    [Fact]
    public void EnforceScope_EmptyEvidence_ReturnsEmptyEvidenceWithoutViolation()
    {
        var scoped = EvidenceSecurity.EnforceScope(
            new EvidenceSearchResult([], []),
            "northstar-bank",
            "silverline-payments");

        scoped.HadOutOfScopeContent.ShouldBeFalse();
        scoped.Evidence.Documents.ShouldBeEmpty();
        scoped.Evidence.Facts.ShouldBeEmpty();
    }

    private static EvidenceDocument CreateDocument(string documentId, string tenantId, string vendorId) =>
        new(
            documentId,
            tenantId,
            vendorId,
            EvidenceDocumentType.Policy,
            UntrustedText.FromExternalSource("External evidence prose."));

    private static EvidenceFact CreateSecurityEvidenceRequiredFact(string sourceDocumentId, string tenantId, string vendorId) =>
        new(tenantId, vendorId, sourceDocumentId, EvidenceFactType.SecurityEvidenceRequired);
}
