using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Api.Dtos;

/// <summary>The untrusted body of a workflow request. Question text is carried but never decided upon.</summary>
public sealed record WorkflowRequest(
    string? VendorId,
    string? Question,
    WorkflowAction RequestedAction,
    string? ApprovalId = null);

/// <summary>
/// The stable wire contract. Risk level and action status are written out as explicit strings rather
/// than serialized enums, so renaming a Core member cannot silently change the published API.
/// </summary>
public sealed record WorkflowResponse(
    Guid WorkflowId,
    string RiskLevel,
    string Recommendation,
    IReadOnlyList<RiskReason> Reasons,
    IReadOnlyList<Citation> Citations,
    IReadOnlyList<MissingEvidenceItem> MissingEvidence,
    bool RequiresApproval,
    string ActionStatus,
    IReadOnlyList<Guid> AuditEventIds,
    IReadOnlyList<QuarantineNote> Warnings)
{
    /// <summary>Maps a framework-independent Core result to its wire representation.</summary>
    public static WorkflowResponse FromCore(WorkflowRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new WorkflowResponse(
            result.WorkflowId,
            result.RiskLevel switch
            {
                Core.Domain.Risk.RiskLevel.Unknown => "unknown",
                Core.Domain.Risk.RiskLevel.Low => "low",
                Core.Domain.Risk.RiskLevel.Medium => "medium",
                Core.Domain.Risk.RiskLevel.High => "high",
                _ => throw new ArgumentOutOfRangeException(nameof(result))
            },
            result.Recommendation,
            result.Reasons,
            result.Citations,
            result.MissingEvidence,
            result.RequiresApproval,
            result.ActionStatus switch
            {
                Core.Contracts.Workflow.ActionStatus.BlockedInvalidRequest => "blocked_invalid_request",
                Core.Contracts.Workflow.ActionStatus.BlockedUnauthorized => "blocked_unauthorized",
                Core.Contracts.Workflow.ActionStatus.DeniedUnknownSubject => "denied_unknown_subject",
                Core.Contracts.Workflow.ActionStatus.BlockedEvidenceUnavailable => "blocked_evidence_unavailable",
                Core.Contracts.Workflow.ActionStatus.BlockedPendingApproval => "blocked_pending_approval",
                Core.Contracts.Workflow.ActionStatus.Executed => "executed",
                _ => throw new ArgumentOutOfRangeException(nameof(result))
            },
            result.AuditEventIds,
            result.Warnings);
    }
}
