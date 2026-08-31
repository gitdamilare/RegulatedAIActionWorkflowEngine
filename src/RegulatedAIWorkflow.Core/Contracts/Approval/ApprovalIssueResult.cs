namespace RegulatedAIWorkflow.Core.Contracts.Approval;

/// <summary>
/// The issuer's verdict on a request to record an approval, with the stored record when one was created.
/// Shaped like <see cref="ApprovalDecision"/> on purpose: no decision point in this codebase answers with
/// a bare null, because a null cannot say which of several refusals happened.
/// </summary>
public sealed record ApprovalIssueResult(ApprovalIssueOutcome Outcome, ApprovalRecord? Approval)
{
    /// <summary>Whether an approval was recorded.</summary>
    public bool IsIssued => Outcome is ApprovalIssueOutcome.Issued;
}

/// <summary>Why an approval was or was not recorded.</summary>
public enum ApprovalIssueOutcome
{
    /// <summary>The approval was stored.</summary>
    Issued,

    /// <summary>The request was malformed. This is the caller's request shape, not a permission problem.</summary>
    InvalidRequest,

    /// <summary>The approver's role may not approve the requested action.</summary>
    ApproverRoleInsufficient
}
