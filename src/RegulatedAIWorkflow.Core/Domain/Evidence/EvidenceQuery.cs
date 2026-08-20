namespace RegulatedAIWorkflow.Core.Domain.Evidence;

/// <summary>
/// Defines the tenant and vendor scope for evidence retrieval.
/// </summary>
/// <param name="TenantId">The tenant that owns the evidence.</param>
/// <param name="VendorId">The vendor to which the evidence applies.</param>
public sealed record EvidenceQuery(string TenantId, string VendorId);
