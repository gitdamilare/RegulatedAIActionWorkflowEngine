using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Ports;

namespace RegulatedAIWorkflow.Infrastructure.Evidence;

/// <summary>Retrieves evidence scoped to exactly one tenant and vendor.</summary>
public sealed class InMemoryEvidenceRepository : IEvidenceRepository
{
    /// <inheritdoc />
    public Task<IReadOnlyList<EvidenceDocument>> SearchEvidenceAsync(
        EvidenceQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<EvidenceDocument> documents = InMemoryEvidenceData.Documents
            .Where(query.Covers)
            .ToArray();

        return Task.FromResult(documents);
    }
}
