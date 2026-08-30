namespace RegulatedAIWorkflow.Core.Contracts.Audit;

/// <summary>
/// Reason codes the orchestrator and the approval gate emit. Risk reason and missing-evidence codes are
/// owned by <see cref="Application.DeterministicRiskEvaluator"/>; each vocabulary has exactly one owner.
/// </summary>
public static class WorkflowAuditCodes
{
    /// <summary>The request failed validation.</summary>
    public const string InvalidRequest = "INVALID_REQUEST";

    /// <summary>The actor's role may not attempt the requested action.</summary>
    public const string RoleNotAuthorized = "ROLE_NOT_AUTHORIZED";

    /// <summary>No evidence exists for this tenant and vendor.</summary>
    public const string UnknownSubject = "UNKNOWN_SUBJECT";

    /// <summary>No approval id was presented for an action that requires one.</summary>
    public const string ApprovalMissing = "APPROVAL_MISSING";

    /// <summary>No approval with the presented id exists in this tenant.</summary>
    public const string ApprovalNotFound = "APPROVAL_NOT_FOUND";

    /// <summary>The approval was issued for a different vendor or action.</summary>
    public const string ApprovalMismatch = "APPROVAL_MISMATCH";

    /// <summary>The requester is the approver.</summary>
    public const string ApprovalSelfApproval = "APPROVAL_SELF_APPROVAL";

    /// <summary>The executor call was outstanding when the run failed.</summary>
    public const string ExecutionOutcomeUnknown = "EXECUTION_OUTCOME_UNKNOWN";
}
