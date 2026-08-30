using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Core.Contracts.Audit;

/// <summary>
/// A workflow audit record. Every field is a server-owned identifier, enum, or code: there is no field
/// a question, a snippet, or an exception message could occupy, so the audit trail cannot carry prose.
/// </summary>
public sealed record AuditEvent(
    Guid EventId,
    Guid WorkflowId,
    DateTimeOffset TimestampUtc,
    string? TenantId,
    string? ActorUserId,
    UserRole ActorRole,
    string? VendorId,
    AuditEventType EventType,
    WorkflowAction RequestedAction,
    RiskLevel? RiskLevel,
    AuditOutcome Outcome,
    IReadOnlyList<string> ReferencedDocumentIds,
    IReadOnlyList<string> ReasonCodes,
    string? ApprovalId,
    string? ApproverUserId);

/// <summary>
/// Every run writes exactly one of each, in this order. The attempt is always written and awaited
/// before any regulated effect can begin.
/// </summary>
public enum AuditEventType
{
    /// <summary>The action was attempted. Written before the executor is reached.</summary>
    ActionAttempt,

    /// <summary>The run reached a terminal outcome.</summary>
    WorkflowCompleted
}

/// <summary>The terminal disposition recorded on a workflow's audit events.</summary>
public enum AuditOutcome
{
    /// <summary>The request failed validation and reached nothing.</summary>
    InvalidRequest,

    /// <summary>The role may not attempt this action. No evidence was retrieved.</summary>
    BlockedUnauthorized,

    /// <summary>No such subject in this tenant. Indistinguishable from a cross-tenant subject.</summary>
    DeniedUnknownSubject,

    /// <summary>High risk with no matching recorded approval. The executor was not called.</summary>
    BlockedPendingApproval,

    /// <summary>Every gate passed. Recorded before the effect.</summary>
    AuthorizedForExecution,

    /// <summary>The effect completed.</summary>
    Executed,

    /// <summary>The executor call was outstanding when the run failed, so the effect may have happened.</summary>
    ExecutionOutcomeUnknown,

    /// <summary>The run failed before the executor was reached, so no effect occurred.</summary>
    Failed
}
