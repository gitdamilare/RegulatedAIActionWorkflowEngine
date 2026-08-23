namespace RegulatedAIWorkflow.Core.Contracts.Audit;

/// <summary>
/// Provides the stable workflow-level reason codes written to audit events.
/// </summary>
public static class WorkflowAuditCodes
{
    /// <summary>The request failed bounded validation.</summary>
    public const string InvalidRequest = "INVALID_REQUEST";

    /// <summary>The caller's role may not attempt the requested action.</summary>
    public const string RoleNotAuthorized = "ROLE_NOT_AUTHORIZED";

    /// <summary>Retrieved evidence violated the requested tenant or vendor scope.</summary>
    public const string EvidenceScopeViolation = "EVIDENCE_SCOPE_VIOLATION";

    /// <summary>One or more assessment citations could not be verified.</summary>
    public const string CitationVerificationFailed = "CITATION_VERIFICATION_FAILED";

    /// <summary>Scoped evidence was empty or the assessment declared it ambiguous.</summary>
    public const string EvidenceGateFailed = "EVIDENCE_GATE_FAILED";

    /// <summary>The principal's role cannot issue the requested approval.</summary>
    public const string ApproverRoleInsufficient = "APPROVER_ROLE_INSUFFICIENT";

    /// <summary>No evidence exists for the requested vendor.</summary>
    public const string VendorNotFound = "VENDOR_NOT_FOUND";

    /// <summary>No approval identifier was supplied.</summary>
    public const string ApprovalMissing = "APPROVAL_MISSING";

    /// <summary>No tenant-scoped approval matched the supplied identifier.</summary>
    public const string ApprovalNotFound = "APPROVAL_NOT_FOUND";

    /// <summary>The stored approval applies to a different action.</summary>
    public const string ApprovalActionMismatch = "APPROVAL_ACTION_MISMATCH";

    /// <summary>The stored approval applies to a different vendor.</summary>
    public const string ApprovalVendorMismatch = "APPROVAL_VENDOR_MISMATCH";

    /// <summary>The risk policy changed after approval issuance.</summary>
    public const string ApprovalPolicySuperseded = "APPROVAL_POLICY_SUPERSEDED";

    /// <summary>The evidence or typed facts changed after approval issuance.</summary>
    public const string ApprovalEvidenceSuperseded = "APPROVAL_EVIDENCE_SUPERSEDED";

    /// <summary>The approval's issue time is still in the future.</summary>
    public const string ApprovalNotYetValid = "APPROVAL_NOT_YET_VALID";

    /// <summary>The approval has reached its expiry time.</summary>
    public const string ApprovalExpired = "APPROVAL_EXPIRED";

    /// <summary>The requester and approver have the same identity.</summary>
    public const string ApprovalSelfApproval = "APPROVAL_SELF_APPROVAL";

    /// <summary>The stored approver role cannot approve the action.</summary>
    public const string ApprovalWrongRole = "APPROVAL_WRONG_ROLE";
}
