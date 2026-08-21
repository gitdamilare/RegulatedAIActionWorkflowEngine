using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Core.Application.Risk;

/// <summary>
/// Provides policy rules with a typed, prose-free view of an evaluation request.
/// </summary>
internal sealed class RiskRuleContext
{
    private readonly HashSet<EvidenceFactType> factTypes;

    public RiskRuleContext(RiskEvaluationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        Facts = input.Facts;
        HasScopedEvidence = input.HasScopedEvidence;
        factTypes = input.Facts
            .Select(fact => fact.FactType)
            .ToHashSet();
    }

    /// <summary>The source-attributed typed facts available to policy.</summary>
    public IReadOnlyList<EvidenceFact> Facts { get; }

    /// <summary>Whether trustworthy evidence survived scope enforcement.</summary>
    public bool HasScopedEvidence { get; }

    /// <summary>Whether the vendor processes payment data.</summary>
    public bool ProcessesPaymentData => Has(EvidenceFactType.ProcessesPaymentData);

    /// <summary>Whether the vendor handles sensitive data.</summary>
    public bool ContainsSensitiveData => Has(EvidenceFactType.ContainsSensitiveData);

    /// <summary>Checks for a normalized fact without exposing evidence prose.</summary>
    public bool Has(EvidenceFactType factType) => factTypes.Contains(factType);
}
