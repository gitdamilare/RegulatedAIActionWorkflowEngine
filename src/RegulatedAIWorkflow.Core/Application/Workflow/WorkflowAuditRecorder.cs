using RegulatedAIWorkflow.Core.Contracts.Audit;
using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Risk;
using RegulatedAIWorkflow.Core.Ports;

namespace RegulatedAIWorkflow.Core.Application.Workflow;

/// <summary>
/// Accumulates the safe, structured facts an audit event needs, and writes them. Constructed before
/// validation so that even a malformed request produces an auditable record. Writes are never passed the
/// caller's cancellation token: a cancelled request must still leave a trail.
/// </summary>
internal sealed class WorkflowAuditRecorder(Guid workflowId, IAuditSink auditSink, TimeProvider timeProvider)
{
    private readonly List<Guid> eventIds = [];

    internal IReadOnlyList<Guid> EventIds => eventIds;

    internal string? TenantId { get; set; }

    internal string? ActorUserId { get; set; }

    internal UserRole ActorRole { get; set; }

    internal string? VendorId { get; set; }

    internal WorkflowAction RequestedAction { get; set; }

    internal RiskLevel? RiskLevel { get; set; }

    internal IReadOnlyList<string> ReferencedDocumentIds { get; set; } = [];

    internal IReadOnlyList<string> ReasonCodes { get; set; } = [];

    internal IReadOnlyList<QuarantineNote> Quarantined { get; set; } = [];

    internal string? ApprovalId { get; set; }

    internal string? ApproverUserId { get; set; }

    internal async Task WriteAsync(AuditEventType eventType, AuditOutcome outcome)
    {
        var eventId = Guid.CreateVersion7();
        await auditSink.WriteAuditEventAsync(
            new AuditEvent(
                eventId,
                workflowId,
                timeProvider.GetUtcNow().ToUniversalTime(),
                TenantId,
                ActorUserId,
                ActorRole,
                VendorId,
                eventType,
                RequestedAction,
                RiskLevel,
                outcome,
                ReferencedDocumentIds,
                ReasonCodes,
                Quarantined,
                ApprovalId,
                ApproverUserId),
            CancellationToken.None);
        eventIds.Add(eventId);
    }

    /// <summary>Writes both events for a run that ends without reaching the executor.</summary>
    internal async Task<WorkflowRunResult> CompleteAsync(WorkflowRunResult result, AuditOutcome outcome)
    {
        await WriteAsync(AuditEventType.ActionAttempt, outcome);
        await WriteAsync(AuditEventType.WorkflowCompleted, outcome);
        return result with { AuditEventIds = EventIds };
    }
}
