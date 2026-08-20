namespace RegulatedAIWorkflow.Core.Domain.Evidence;

/// <summary>
/// Identifies the business purpose of an evidence document.
/// </summary>
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
