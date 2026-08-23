namespace RegulatedAIWorkflow.Core.Domain.Approval;

/// <summary>
/// Returns a structured approval outcome and a safely scoped matching record when available.
/// </summary>
public sealed record ApprovalDecision(ApprovalOutcome Outcome, ApprovalRecord? Approval)
{
    /// <summary>Whether every approval binding passed.</summary>
    public bool IsApproved => Outcome is ApprovalOutcome.Valid;
}
