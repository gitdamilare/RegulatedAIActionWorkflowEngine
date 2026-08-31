using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using RegulatedAIWorkflow.Core.Contracts.Workflow;

namespace RegulatedAIWorkflow.Core.Domain.Evidence;

/// <summary>
/// The identity of one evidence set, as the approver saw it. An approval bound to this hash stops
/// authorizing the moment a document is added, removed, or edited underneath it, which is the difference
/// between approving a decision and approving a vendor forever.
/// </summary>
public static class EvidenceSetHash
{
    /// <summary>
    /// Computes the binding. Ordering is normalized so retrieval order cannot change the answer, and every
    /// field is length-prefixed so no combination of values can be forged by rearranging delimiters.
    /// <para>
    /// Note what is absent: the caller's question. Hashing it would tie an approval to one phrasing, so
    /// approving with one wording and acting with another would report a supersession that never happened.
    /// The binding is over the evidence, not over how it was asked for.
    /// </para>
    /// </summary>
    public static string Compute(
        EvidenceQuery scope,
        WorkflowAction action,
        IReadOnlyList<EvidenceDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(documents);

        var canonical = new StringBuilder();
        Append(canonical, scope.TenantId);
        Append(canonical, scope.VendorId);
        Append(canonical, action.ToString());

        foreach (var document in documents.OrderBy(item => item.DocumentId, StringComparer.Ordinal))
        {
            Append(canonical, document.DocumentId);
            Append(canonical, ((int)document.DocumentType).ToString(CultureInfo.InvariantCulture));
            Append(canonical, document.UntrustedSnippet.Fingerprint());

            foreach (var factType in document.FactTypes.OrderBy(item => (int)item))
            {
                Append(canonical, ((int)factType).ToString(CultureInfo.InvariantCulture));
            }
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static void Append(StringBuilder builder, string value) =>
        builder.Append(value.Length).Append(':').Append(value).Append('|');
}
