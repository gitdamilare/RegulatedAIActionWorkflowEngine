namespace RegulatedAIWorkflow.Core.Domain.Evidence;

/// <summary>
/// Identifies a normalized fact that deterministic policy may consume.
/// </summary>
public enum EvidenceFactType
{
    /// <summary>The vendor processes payment data.</summary>
    ProcessesPaymentData,

    /// <summary>The vendor handles sensitive data.</summary>
    ContainsSensitiveData,

    /// <summary>The applicable policy requires security evidence.</summary>
    SecurityEvidenceRequired,

    /// <summary>The contract lacks a required breach-notification term.</summary>
    BreachNotificationMissing,

    /// <summary>The contract contains a breach-notification term.</summary>
    BreachNotificationPresent,

    /// <summary>A SOC 2 report is available.</summary>
    Soc2Available,

    /// <summary>A data-retention schedule is available.</summary>
    DataRetentionScheduleAvailable
}
