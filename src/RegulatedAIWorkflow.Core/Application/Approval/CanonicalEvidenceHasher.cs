using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using RegulatedAIWorkflow.Core.Domain.Evidence;

namespace RegulatedAIWorkflow.Core.Application.Approval;

/// <summary>
/// Computes an order-independent binding for scoped documents, typed facts, and policy.
/// </summary>
public static class CanonicalEvidenceHasher
{
    /// <summary>Computes a lowercase SHA-256 evidence-set fingerprint.</summary>
    public static string Compute(
        string tenantId,
        string vendorId,
        EvidenceSearchResult evidence,
        string riskPolicyVersion)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var canonical = new StringBuilder();
        Append(canonical, tenantId);
        Append(canonical, vendorId);
        Append(canonical, riskPolicyVersion);

        foreach (var document in evidence.Documents
                     .OrderBy(item => item.DocumentId, StringComparer.Ordinal))
        {
            Append(canonical, document.DocumentId);
            Append(canonical, ((int)document.DocumentType).ToString(CultureInfo.InvariantCulture));
            Append(canonical, document.UntrustedSnippet.Fingerprint());
        }

        foreach (var fact in evidence.Facts
                     .OrderBy(item => item.SourceDocumentId, StringComparer.Ordinal)
                     .ThenBy(item => item.FactType))
        {
            Append(canonical, fact.SourceDocumentId);
            Append(canonical, ((int)fact.FactType).ToString(CultureInfo.InvariantCulture));
        }

        return Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static void Append(StringBuilder builder, string value) =>
        builder.Append(value.Length).Append(':').Append(value).Append('|');
}
