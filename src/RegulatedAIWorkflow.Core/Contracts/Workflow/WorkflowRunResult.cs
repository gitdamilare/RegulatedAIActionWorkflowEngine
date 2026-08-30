using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Core.Contracts.Workflow;

/// <summary>The constrained result of one workflow run. Every field is server-authored.</summary>
public sealed record WorkflowRunResult(
    Guid WorkflowId,
    RiskLevel RiskLevel,
    string Recommendation,
    IReadOnlyList<RiskReason> Reasons,
    IReadOnlyList<Citation> Citations,
    IReadOnlyList<MissingEvidenceItem> MissingEvidence,
    bool RequiresApproval,
    ActionStatus ActionStatus,
    IReadOnlyList<Guid> AuditEventIds)
{
    private const string UnknownSubjectRecommendation = "No such subject in this tenant.";
    private const string ApprovedRecommendation =
        "Proceeded under recorded approval. The assessment remains high and any evidence gaps below are still outstanding.";

    /// <summary>A refusal that discloses nothing, because nothing was assessed.</summary>
    public static WorkflowRunResult Refused(Guid workflowId, ActionStatus status) =>
        new(workflowId, RiskLevel.Unknown, string.Empty, [], [], [], RequiresApproval: false, status, []);

    /// <summary>
    /// The denial for a subject this tenant does not have. Deliberately identical whether the vendor
    /// exists in another tenant or nowhere at all.
    /// </summary>
    public static WorkflowRunResult UnknownSubject(Guid workflowId, string reasonCode) =>
        new(
            workflowId,
            RiskLevel.Unknown,
            UnknownSubjectRecommendation,
            [new RiskReason(reasonCode, UnknownSubjectRecommendation)],
            [],
            [],
            RequiresApproval: false,
            ActionStatus.DeniedUnknownSubject,
            []);

    /// <summary>
    /// A completed assessment. Approval changes the recommendation and the status; it never lowers the
    /// risk level or clears the evidence gaps, because the gaps are still real after someone signs off.
    /// </summary>
    public static WorkflowRunResult Assessed(
        Guid workflowId,
        RiskEvaluation evaluation,
        IReadOnlyList<Citation> citations,
        ActionStatus status) =>
        new(
            workflowId,
            evaluation.RiskLevel,
            status is ActionStatus.Executed && evaluation.RequiresApproval
                ? ApprovedRecommendation
                : evaluation.Recommendation,
            evaluation.Reasons,
            citations,
            evaluation.MissingEvidence,
            evaluation.RequiresApproval,
            status,
            []);
}

/// <summary>
/// A supporting document and the untrusted snippet it contributed. This is the only path by which
/// external prose reaches a caller, and it is display data: nothing branches on it.
/// </summary>
public sealed record Citation(string DocumentId, string Snippet);

/// <summary>What happened to the requested action.</summary>
public enum ActionStatus
{
    /// <summary>The request failed validation.</summary>
    BlockedInvalidRequest,

    /// <summary>The role may not attempt this action.</summary>
    BlockedUnauthorized,

    /// <summary>No such subject in this tenant.</summary>
    DeniedUnknownSubject,

    /// <summary>High risk with no matching recorded approval. The executor was not called.</summary>
    BlockedPendingApproval,

    /// <summary>The action ran.</summary>
    Executed
}
