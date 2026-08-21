namespace RegulatedAIWorkflow.Core.Contracts.Audit;

/// <summary>
/// Identifies the workflow transition represented by an audit event.
/// </summary>
public enum AuditEventType
{
    /// <summary>A regulated action was attempted and blocked.</summary>
    ActionAttempt,

    /// <summary>The workflow reached a terminal state.</summary>
    WorkflowCompleted
}
