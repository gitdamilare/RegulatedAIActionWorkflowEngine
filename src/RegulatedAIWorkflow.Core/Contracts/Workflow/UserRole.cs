namespace RegulatedAIWorkflow.Core.Contracts.Workflow;

/// <summary>
/// Describes a caller's role for later authorization decisions.
/// </summary>
public enum UserRole
{
    /// <summary>No recognized role was supplied.</summary>
    Unknown,

    /// <summary>A read-only user.</summary>
    Viewer,

    /// <summary>A user responsible for procurement decisions.</summary>
    ProcurementManager,

    /// <summary>A user responsible for compliance review.</summary>
    ComplianceOfficer,

    /// <summary>A user permitted to provide an independent risk approval.</summary>
    RiskApprover
}
