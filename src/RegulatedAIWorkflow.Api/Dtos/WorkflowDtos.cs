using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Risk;
using CoreActionStatus = RegulatedAIWorkflow.Core.Contracts.Workflow.ActionStatus;
using CoreRiskLevel = RegulatedAIWorkflow.Core.Domain.Risk.RiskLevel;

namespace RegulatedAIWorkflow.Api.Dtos;

/// <summary>Contains the untrusted body of a workflow request.</summary>
public sealed record WorkflowRequest(
    string? VendorId,
    string? Question,
    WorkflowAction RequestedAction,
    string? ApprovalId = null);

/// <summary>Contains the stable HTTP representation of a workflow result.</summary>
public sealed record WorkflowResponse(
    Guid WorkflowId,
    string RiskLevel,
    string Recommendation,
    IReadOnlyList<RiskReason> Reasons,
    IReadOnlyList<Citation> Citations,
    IReadOnlyList<MissingEvidenceItem> MissingEvidence,
    bool RequiresApproval,
    string ActionStatus,
    IReadOnlyList<Guid> AuditEventIds)
{
    /// <summary>Maps a framework-independent Core result to its wire representation.</summary>
    public static WorkflowResponse FromCore(WorkflowRunResult result) =>
        new(
            result.WorkflowId,
            result.RiskLevel switch
            {
                CoreRiskLevel.Unknown => "unknown",
                CoreRiskLevel.Low => "low",
                CoreRiskLevel.Medium => "medium",
                CoreRiskLevel.High => "high",
                _ => throw new ArgumentOutOfRangeException(nameof(result))
            },
            result.Recommendation,
            result.Reasons,
            result.Citations,
            result.MissingEvidence,
            result.RequiresApproval,
            result.ActionStatus switch
            {
                CoreActionStatus.BlockedInvalidRequest => "blocked_invalid_request",
                CoreActionStatus.BlockedUnauthorized => "blocked_unauthorized",
                CoreActionStatus.DeniedUnknownSubject => "denied_unknown_subject",
                CoreActionStatus.BlockedPendingApproval => "blocked_pending_approval",
                CoreActionStatus.BlockedEvidenceUnavailable => "blocked_evidence_unavailable",
                CoreActionStatus.BlockedExecutionUnavailable => "blocked_execution_unavailable",
                CoreActionStatus.Executed => "executed",
                _ => throw new ArgumentOutOfRangeException(nameof(result))
            },
            result.AuditEventIds);
}
