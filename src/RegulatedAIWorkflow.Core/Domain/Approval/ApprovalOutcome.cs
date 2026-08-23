namespace RegulatedAIWorkflow.Core.Domain.Approval;

/// <summary>
/// Describes why a stored approval did or did not authorize the current request.
/// </summary>
public enum ApprovalOutcome
{
    Missing,
    NotFound,
    ActionMismatch,
    VendorMismatch,
    PolicySuperseded,
    EvidenceSuperseded,
    NotYetValid,
    Expired,
    SelfApproval,
    WrongRole,
    Valid
}
