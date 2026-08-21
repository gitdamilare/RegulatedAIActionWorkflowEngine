using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Core.Contracts.Workflow;

/// <summary>
/// Returns a structured workflow decision without exposing raw evidence prose.
/// </summary>
/// <param name="WorkflowId">The unique identifier for this workflow run.</param>
/// <param name="RiskLevel">The deterministic risk assessment.</param>
/// <param name="Recommendation">The policy-authored recommended response.</param>
/// <param name="Reasons">The structured reasons supporting the assessment.</param>
/// <param name="Citations">Verified citations safe to return to the caller.</param>
/// <param name="MissingEvidence">Evidence gaps considered by the assessment.</param>
/// <param name="RequiresApproval">Whether an independent approval is required.</param>
/// <param name="ActionStatus">The resulting blocked action status.</param>
/// <param name="AuditEventIds">The identifiers of audit events persisted for this workflow response.</param>
public sealed record WorkflowRunResult(
    Guid WorkflowId,
    RiskLevel RiskLevel,
    string Recommendation,
    IReadOnlyList<RiskReason> Reasons,
    IReadOnlyList<Citation> Citations,
    IReadOnlyList<MissingEvidenceItem> MissingEvidence,
    bool RequiresApproval,
    ActionStatus ActionStatus,
    IReadOnlyList<Guid> AuditEventIds);
