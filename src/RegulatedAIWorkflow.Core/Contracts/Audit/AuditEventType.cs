namespace RegulatedAIWorkflow.Core.Contracts.Audit;

/// <summary>
/// Identifies the workflow transition represented by an audit event.
/// </summary>
public enum AuditEventType
{
    /// <summary>A stored approval was issued, accepted, or rejected.</summary>
    ApprovalDecision,

    /// <summary>A regulated action was attempted and either blocked or authorized.</summary>
    ActionAttempt,

    /// <summary>A regulated action executor reported its result.</summary>
    ActionExecution,

    /// <summary>The workflow reached a terminal state.</summary>
    WorkflowCompleted
}
