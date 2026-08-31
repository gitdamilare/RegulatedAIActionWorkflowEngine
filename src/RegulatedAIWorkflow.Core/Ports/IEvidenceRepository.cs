using RegulatedAIWorkflow.Core.Domain.Evidence;

namespace RegulatedAIWorkflow.Core.Ports;

/// <summary>
/// Retrieves evidence. Scope is a parameter of the query, not a filter applied to a wider result, so an
/// adapter is never asked to hand Core anything it must then discard.
/// </summary>
public interface IEvidenceRepository
{
    /// <summary>Returns the documents held for exactly the requested scope.</summary>
    Task<IReadOnlyList<EvidenceDocument>> SearchEvidenceAsync(
        EvidenceQuery query,
        CancellationToken cancellationToken);
}
