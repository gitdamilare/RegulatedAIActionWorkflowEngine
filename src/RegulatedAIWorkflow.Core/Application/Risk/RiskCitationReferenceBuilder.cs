using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Core.Application.Risk;

/// <summary>
/// Builds deterministic document references exclusively from rule-selected fact sources.
/// </summary>
internal static class RiskCitationReferenceBuilder
{
    public static List<RiskCitationReference> Build(
        IReadOnlyList<EvidenceFact> facts,
        IReadOnlyList<RiskRuleOutcome> outcomes)
    {
        var retainedDocumentIds = new HashSet<string>(StringComparer.Ordinal);
        var references = new List<RiskCitationReference>();

        foreach (var outcome in outcomes)
        {
            if (outcome.CitationSourceFactType is not { } citationSourceFactType)
            {
                continue;
            }

            var sourceDocumentIds = facts
                .Where(fact => fact.FactType == citationSourceFactType)
                .Select(fact => fact.SourceDocumentId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal);

            foreach (var sourceDocumentId in sourceDocumentIds)
            {
                if (retainedDocumentIds.Add(sourceDocumentId))
                {
                    references.Add(new RiskCitationReference(sourceDocumentId));
                }
            }
        }

        return references;
    }
}
