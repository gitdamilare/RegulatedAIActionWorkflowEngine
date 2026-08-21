namespace RegulatedAIWorkflow.Core.Contracts.Audit;

/// <summary>
/// Provides a fixed, non-sensitive outcome for a workflow audit event.
/// </summary>
public enum AuditOutcome
{
    /// <summary>The request failed bounded validation.</summary>
    InvalidRequest,

    /// <summary>The caller was not authorized for the action.</summary>
    BlockedUnauthorized,

    /// <summary>Trustworthy evidence was unavailable or inconsistent.</summary>
    BlockedEvidenceUnavailable,

    /// <summary>A valid high-risk decision requires independent approval.</summary>
    BlockedPendingApproval,

    /// <summary>A valid lower-risk decision remains blocked because execution is unavailable.</summary>
    BlockedExecutionUnavailable,

    /// <summary>The workflow failed unexpectedly.</summary>
    Failed
}
