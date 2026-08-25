namespace RegulatedAIWorkflow.Core.Contracts.Audit;

/// <summary>
/// Provides a fixed, non-sensitive outcome for a workflow audit event.
/// </summary>
public enum AuditOutcome
{
    /// <summary>A new approval was bound and stored.</summary>
    ApprovalRecorded,

    /// <summary>A stored approval matched every current binding.</summary>
    ApprovalAccepted,

    /// <summary>An approval issuance or verification was rejected.</summary>
    ApprovalRejected,

    /// <summary>The request failed bounded validation.</summary>
    InvalidRequest,

    /// <summary>The caller was not authorized for the action.</summary>
    BlockedUnauthorized,

    /// <summary>Trustworthy evidence was unavailable or inconsistent.</summary>
    BlockedEvidenceUnavailable,

    /// <summary>A valid high-risk decision requires independent approval.</summary>
    BlockedPendingApproval,

    /// <summary>The executor reported that no regulated effect occurred.</summary>
    BlockedExecutionUnavailable,

    /// <summary>Every applicable gate passed and execution may begin.</summary>
    AuthorizedForExecution,

    /// <summary>The regulated action executor reported success.</summary>
    Executed,

    /// <summary>The executor call ended without a definitive regulated-effect outcome.</summary>
    ExecutionOutcomeUnknown,

    /// <summary>The workflow failed unexpectedly.</summary>
    Failed,

    /// <summary>No subject exists within the caller's tenant scope.</summary>
    DeniedUnknownSubject
}
