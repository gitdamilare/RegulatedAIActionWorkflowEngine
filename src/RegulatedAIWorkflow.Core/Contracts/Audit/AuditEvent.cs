using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Core.Contracts.Audit;

/// <summary>
/// Captures a safe structured workflow event without request or evidence prose.
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
    IReadOnlyList<string> MissingEvidenceCodes,
    string? PolicyVersion,
    string? ApprovalId,
    string? ApproverUserId);
