namespace RegulatedAIWorkflow.Core.Domain.Evidence;

/// <summary>
/// Returns evidence documents together with their normalized facts.
/// </summary>
/// <param name="Documents">The retrieved evidence documents.</param>
/// <param name="Facts">The normalized facts attributed to those documents.</param>
public sealed record EvidenceSearchResult(
    IReadOnlyList<EvidenceDocument> Documents,
    IReadOnlyList<EvidenceFact> Facts);
