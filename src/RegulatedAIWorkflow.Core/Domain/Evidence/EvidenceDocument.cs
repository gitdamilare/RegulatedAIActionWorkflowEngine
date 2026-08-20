namespace RegulatedAIWorkflow.Core.Domain.Evidence;

/// <summary>
/// Represents a tenant-scoped evidence document whose prose remains untrusted.
/// </summary>
/// <param name="DocumentId">The stable document identifier.</param>
/// <param name="TenantId">The tenant that owns the document.</param>
/// <param name="VendorId">The vendor to which the document applies.</param>
/// <param name="DocumentType">The document's business purpose.</param>
/// <param name="UntrustedSnippet">External prose that cannot directly influence policy.</param>
public sealed record EvidenceDocument(
    string DocumentId,
    string TenantId,
    string VendorId,
    EvidenceDocumentType DocumentType,
    UntrustedText UntrustedSnippet);
