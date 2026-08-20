namespace RegulatedAIWorkflow.Core.Domain.Evidence;

/// <summary>
/// Represents a normalized, source-attributed fact available to deterministic policy.
/// </summary>
/// <param name="TenantId">The tenant that owns the fact.</param>
/// <param name="VendorId">The vendor to which the fact applies.</param>
/// <param name="SourceDocumentId">The evidence document supporting the fact.</param>
/// <param name="FactType">The normalized fact classification.</param>
public sealed record EvidenceFact(
    string TenantId,
    string VendorId,
    string SourceDocumentId,
    EvidenceFactType FactType);
