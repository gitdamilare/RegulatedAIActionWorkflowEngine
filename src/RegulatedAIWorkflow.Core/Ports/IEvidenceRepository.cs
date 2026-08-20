using RegulatedAIWorkflow.Core.Domain.Evidence;

namespace RegulatedAIWorkflow.Core.Ports;

/// <summary>
/// Retrieves evidence within an explicit tenant and vendor scope.
/// </summary>
public interface IEvidenceRepository
{
    /// <summary>
    /// Searches for evidence matching the requested scope.
    /// </summary>
    /// <param name="query">The tenant and vendor scope to search.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    /// <returns>The retrieved documents and normalized facts.</returns>
    Task<EvidenceSearchResult> SearchEvidenceAsync(
        EvidenceQuery query,
        CancellationToken cancellationToken);
}
