using RegulatedAIWorkflow.Core.Contracts.Audit;

namespace RegulatedAIWorkflow.Core.Contracts.Approval;

/// <summary>The gate's verdict on a presented approval, with the record when one was found.</summary>
public sealed record ApprovalDecision(ApprovalOutcome Outcome, ApprovalRecord? Approval)
{
    /// <summary>Whether every binding passed and the effect may proceed.</summary>
    public bool IsApproved => Outcome is ApprovalOutcome.Approved;

    /// <summary>The audit code for a refusal. An approved decision has none.</summary>
    public string ReasonCode => Outcome switch
    {
        ApprovalOutcome.Missing => WorkflowAuditCodes.ApprovalMissing,
        ApprovalOutcome.NotFound => WorkflowAuditCodes.ApprovalNotFound,
        ApprovalOutcome.Mismatch => WorkflowAuditCodes.ApprovalMismatch,
        ApprovalOutcome.EvidenceSuperseded => WorkflowAuditCodes.ApprovalEvidenceSuperseded,
        ApprovalOutcome.Expired => WorkflowAuditCodes.ApprovalExpired,
        ApprovalOutcome.SelfApproval => WorkflowAuditCodes.ApprovalSelfApproval,
        _ => throw new InvalidOperationException("An approved decision has no rejection code.")
    };
}

/// <summary>Why a presented approval did or did not authorize the current request.</summary>
public enum ApprovalOutcome
{
    /// <summary>No approval id was presented.</summary>
    Missing,

    /// <summary>No approval with that id exists in this tenant.</summary>
    NotFound,

    /// <summary>The approval was issued for a different vendor or action.</summary>
    Mismatch,

    /// <summary>The evidence changed after the approval was granted, so it covers a decision nobody made.</summary>
    EvidenceSuperseded,

    /// <summary>The approval's validity window has closed.</summary>
    Expired,

    /// <summary>The requester is the approver. Separation of duties forbids this.</summary>
    SelfApproval,

    /// <summary>Every binding matched.</summary>
    Approved
}
