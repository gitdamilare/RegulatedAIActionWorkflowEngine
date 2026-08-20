namespace RegulatedAIWorkflow.Core.Domain.Evidence;

/// <summary>
/// Reports evidence after Core scope checks and whether suspect content was removed.
/// </summary>
/// <param name="Evidence">The evidence retained within the requested scope.</param>
/// <param name="HadOutOfScopeContent">Whether retrieved content violated the requested scope.</param>
public sealed record ScopedEvidence(EvidenceSearchResult Evidence, bool HadOutOfScopeContent);
