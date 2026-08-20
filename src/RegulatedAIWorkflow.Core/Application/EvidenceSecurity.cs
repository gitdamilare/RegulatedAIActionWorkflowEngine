using RegulatedAIWorkflow.Core.Domain.Evidence;

namespace RegulatedAIWorkflow.Core.Application;

/// <summary>
/// Applies Core-owned trust-boundary checks to evidence returned by an external repository.
/// </summary>
public static class EvidenceSecurity
{
    /// <summary>
    /// Retains only evidence belonging to the requested tenant and vendor and reports inconsistencies.
    /// </summary>
    /// <param name="retrieved">The evidence returned by a repository.</param>
    /// <param name="tenantId">The tenant identifier that must own every retained item.</param>
    /// <param name="vendorId">The vendor identifier that must apply to every retained item.</param>
    /// <returns>The retained evidence and an indication of any boundary violation.</returns>
    public static ScopedEvidence EnforceScope(
        EvidenceSearchResult retrieved,
        string tenantId,
        string vendorId)
    {
        ArgumentNullException.ThrowIfNull(retrieved);

        var scopedDocuments = retrieved.Documents
            .Where(document =>
                string.Equals(document.TenantId, tenantId, StringComparison.Ordinal) &&
                string.Equals(document.VendorId, vendorId, StringComparison.Ordinal))
            .ToArray();

        var retainedDocumentIds = scopedDocuments
            .Select(document => document.DocumentId)
            .ToHashSet(StringComparer.Ordinal);

        var scopedFacts = retrieved.Facts
            .Where(fact =>
                string.Equals(fact.TenantId, tenantId, StringComparison.Ordinal) &&
                string.Equals(fact.VendorId, vendorId, StringComparison.Ordinal) &&
                retainedDocumentIds.Contains(fact.SourceDocumentId))
            .ToArray();

        var hadOutOfScopeContent =
            scopedDocuments.Length != retrieved.Documents.Count ||
            scopedFacts.Length != retrieved.Facts.Count ||
            retainedDocumentIds.Count != scopedDocuments.Length;

        return new ScopedEvidence(
            new EvidenceSearchResult(scopedDocuments, scopedFacts),
            hadOutOfScopeContent);
    }
}
