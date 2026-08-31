using RegulatedAIWorkflow.Core.Domain.Evidence;

namespace RegulatedAIWorkflow.Core.Application.Risk;

/// <summary>
/// The prose-free view of an evaluation that rules are allowed to see. It answers only two kinds of
/// question: whether a normalized fact is present, and which documents supplied a given fact.
/// </summary>
internal sealed class RiskRuleContext
{
    private readonly IReadOnlyList<EvidenceFact> facts;
    private readonly HashSet<EvidenceFactType> presentFactTypes;

    internal RiskRuleContext(IReadOnlyList<EvidenceFact> facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        this.facts = facts;
        presentFactTypes = facts.Select(fact => fact.FactType).ToHashSet();
    }

    /// <summary>Whether the vendor processes payment data.</summary>
    internal bool ProcessesPaymentData => Has(EvidenceFactType.ProcessesPaymentData);

    /// <summary>Whether the vendor handles sensitive data.</summary>
    internal bool ContainsSensitiveData => Has(EvidenceFactType.ContainsSensitiveData);

    /// <summary>Checks for a normalized fact. The only question a rule may ask of the evidence.</summary>
    internal bool Has(EvidenceFactType factType) => presentFactTypes.Contains(factType);

    /// <summary>
    /// Names the documents that supplied the given facts, in the order the facts were cited and then by
    /// document id, without repeating a document that several facts share.
    /// </summary>
    internal IReadOnlyList<string> SourceDocumentIdsFor(IEnumerable<EvidenceFactType> citedFactTypes)
    {
        var documentIds = new List<string>();

        foreach (var factType in citedFactTypes)
        {
            var sources = facts
                .Where(fact => fact.FactType == factType)
                .Select(fact => fact.SourceDocumentId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal);

            foreach (var documentId in sources)
            {
                if (!documentIds.Contains(documentId, StringComparer.Ordinal))
                {
                    documentIds.Add(documentId);
                }
            }
        }

        return documentIds;
    }
}
