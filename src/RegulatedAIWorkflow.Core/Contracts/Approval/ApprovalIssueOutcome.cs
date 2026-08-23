namespace RegulatedAIWorkflow.Core.Contracts.Approval;

/// <summary>
/// Describes the outcome of an approval issuance attempt.
/// </summary>
public enum ApprovalIssueOutcome
{
    /// <summary>The approval was stored successfully.</summary>
    Issued,

    /// <summary>The request or trusted principal was malformed.</summary>
    InvalidRequest,

    /// <summary>The principal's role cannot approve the requested action.</summary>
    ApproverRoleInsufficient,

    /// <summary>No evidence exists for the requested tenant and vendor.</summary>
    VendorNotFound,

    /// <summary>The evidence repository returned unusable scoped content.</summary>
    EvidenceUnavailable
}
