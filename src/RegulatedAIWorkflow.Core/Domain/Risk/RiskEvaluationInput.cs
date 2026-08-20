using RegulatedAIWorkflow.Core.Domain.Evidence;

namespace RegulatedAIWorkflow.Core.Domain.Risk;

/// <summary>
/// Supplies deterministic risk policy with normalized facts and scope state only.
/// </summary>
/// <param name="Facts">The scoped, source-attributed facts to evaluate.</param>
/// <param name="HasScopedEvidence">Whether trustworthy evidence remains after scope checks.</param>
public sealed record RiskEvaluationInput(
    IReadOnlyList<EvidenceFact> Facts,
    bool HasScopedEvidence);
