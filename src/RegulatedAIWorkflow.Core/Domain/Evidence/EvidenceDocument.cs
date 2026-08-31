namespace RegulatedAIWorkflow.Core.Domain.Evidence;

/// <summary>
/// A tenant-scoped evidence document. <c>FactTypes</c> is server-owned metadata assigned at ingestion and
/// is the only part policy may read; <c>UntrustedSnippet</c> is external prose whose type says so, and
/// which reaches a caller only through an explicit <see cref="UntrustedText.ForDisplay"/> call.
/// </summary>
public sealed record EvidenceDocument(
    string DocumentId,
    string TenantId,
    string VendorId,
    EvidenceDocumentType DocumentType,
    IReadOnlyList<EvidenceFactType> FactTypes,
    UntrustedText UntrustedSnippet);

/// <summary>Identifies the business purpose of an evidence document.</summary>
public enum EvidenceDocumentType
{
    /// <summary>An internal policy document.</summary>
    Policy,

    /// <summary>A contract governing the vendor relationship.</summary>
    Contract,

    /// <summary>Evidence supplied directly by the vendor.</summary>
    VendorSubmission,

    /// <summary>A SOC 2 assurance report.</summary>
    Soc2Report,

    /// <summary>A schedule governing retained data.</summary>
    DataRetentionSchedule
}
