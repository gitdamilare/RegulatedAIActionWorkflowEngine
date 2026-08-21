using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Core.Application.Workflow;

/// <summary>
/// Resolves risk references only against retained, source-linked evidence.
/// </summary>
internal static class VerifiedCitationResolver
{
    internal static bool TryResolve(
        IReadOnlyList<RiskCitationReference> references,
        EvidenceSearchResult scopedEvidence,
        out IReadOnlyList<Citation> citations)
    {
        var documentsById = new Dictionary<string, EvidenceDocument>(StringComparer.Ordinal);
        foreach (var document in scopedEvidence.Documents)
        {
            if (!IsSafeIdentifier(document.DocumentId) ||
                !documentsById.TryAdd(document.DocumentId, document))
            {
                citations = [];
                return false;
            }
        }

        var sourceDocumentIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fact in scopedEvidence.Facts)
        {
            if (!IsSafeIdentifier(fact.SourceDocumentId) ||
                !documentsById.ContainsKey(fact.SourceDocumentId))
            {
                citations = [];
                return false;
            }

            sourceDocumentIds.Add(fact.SourceDocumentId);
        }

        var resolvedDocumentIds = new HashSet<string>(StringComparer.Ordinal);
        var resolvedCitations = new List<Citation>();

        foreach (var reference in references)
        {
            if (!IsSafeIdentifier(reference.DocumentId) ||
                !resolvedDocumentIds.Add(reference.DocumentId) ||
                !sourceDocumentIds.Contains(reference.DocumentId) ||
                !documentsById.TryGetValue(reference.DocumentId, out var document))
            {
                citations = [];
                return false;
            }

            var snippet = document.UntrustedSnippet.ForDisplay();
            if (string.IsNullOrWhiteSpace(snippet))
            {
                citations = [];
                return false;
            }

            resolvedCitations.Add(new Citation(reference.DocumentId, snippet));
        }

        citations = resolvedCitations.ToArray();
        return true;
    }

    private static bool IsSafeIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);
}
