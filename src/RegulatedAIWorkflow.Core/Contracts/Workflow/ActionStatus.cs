namespace RegulatedAIWorkflow.Core.Contracts.Workflow;

/// <summary>
/// Describes why a workflow did not execute an action.
/// </summary>
public enum ActionStatus
{
    /// <summary>The workflow request was malformed or exceeded a supported bound.</summary>
    BlockedInvalidRequest,

    /// <summary>The caller is not authorized to request the action.</summary>
    BlockedUnauthorized,

    /// <summary>The assessed risk requires an independent approval.</summary>
    BlockedPendingApproval,

    /// <summary>Trustworthy evidence was unavailable or inconsistent.</summary>
    BlockedEvidenceUnavailable,

    /// <summary>The evidence was valid, but action execution is not available.</summary>
    BlockedExecutionUnavailable
}
