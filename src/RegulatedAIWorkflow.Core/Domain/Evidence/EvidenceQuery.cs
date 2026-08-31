namespace RegulatedAIWorkflow.Core.Domain.Evidence;

/// <summary>
/// The tenant and vendor scope of one evidence retrieval. It exists so that scope is a value with a name
/// rather than two interchangeable strings, and so that the adapter performing the query and the Core
/// assertion that distrusts its answer share one definition of what "in scope" means.
/// </summary>
public sealed record EvidenceQuery
{
    /// <summary>Creates a scope. An unscoped query is not representable.</summary>
    public EvidenceQuery(string tenantId, string vendorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(vendorId);

        TenantId = tenantId;
        VendorId = vendorId;
    }

    /// <summary>The tenant that owns the evidence.</summary>
    public string TenantId { get; }

    /// <summary>The vendor to which the evidence applies.</summary>
    public string VendorId { get; }

    /// <summary>Whether a document falls inside this scope. The single definition, used on both sides.</summary>
    public bool Covers(EvidenceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return string.Equals(document.TenantId, TenantId, StringComparison.Ordinal) &&
            string.Equals(document.VendorId, VendorId, StringComparison.Ordinal);
    }
}
