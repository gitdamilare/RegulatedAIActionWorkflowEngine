using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Ports;

namespace RegulatedAIWorkflow.Infrastructure.Evidence;

/// <summary>
/// Retrieves tenant- and vendor-scoped evidence from the in-memory corpus.
/// </summary>
public sealed class InMemoryEvidenceRepository : IEvidenceRepository
{
    /// <inheritdoc />
    public Task<EvidenceSearchResult> SearchEvidenceAsync(
        EvidenceQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var documents = InMemoryEvidenceData.Documents
            .Where(document =>
                string.Equals(document.TenantId, query.TenantId, StringComparison.Ordinal) &&
                string.Equals(document.VendorId, query.VendorId, StringComparison.Ordinal))
            .ToArray();

        var facts = InMemoryEvidenceData.Facts
            .Where(fact =>
                string.Equals(fact.TenantId, query.TenantId, StringComparison.Ordinal) &&
                string.Equals(fact.VendorId, query.VendorId, StringComparison.Ordinal))
            .ToArray();

        return Task.FromResult(new EvidenceSearchResult(documents, facts));
    }
}
