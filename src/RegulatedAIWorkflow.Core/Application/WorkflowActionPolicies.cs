using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Core.Application;

/// <summary>The server-owned policy attached to one workflow action.</summary>
internal sealed record WorkflowActionPolicy(
    RiskLevel BaselineRiskLevel,
    RiskReason BaselineRiskReason,
    IReadOnlyList<UserRole> AllowedRequesterRoles,
    IReadOnlyList<UserRole> AllowedApproverRoles);

/// <summary>
/// The registry of actions the server will consider at all. Adding an action means adding one entry
/// here and one enum member; an action with no entry is denied by every question asked below.
/// </summary>
internal static class WorkflowActionPolicies
{
    private static readonly WorkflowActionPolicy MarkVendorApproved =
        new(
            RiskLevel.High,
            new RiskReason(
                "ACTION_MARK_VENDOR_APPROVED_HIGH_RISK",
                "Marking a vendor approved to process payment data is classified as a high-risk action."),
            [UserRole.ProcurementManager, UserRole.ComplianceOfficer],
            [UserRole.RiskApprover]);

    /// <summary>Deny by default: an unknown role or an unregistered action may attempt nothing.</summary>
    internal static bool MayAttempt(UserRole role, WorkflowAction action) =>
        TryGet(action, out var policy) && policy.AllowedRequesterRoles.Contains(role);

    /// <summary>Deny by default: only a role the action names may record an approval for it.</summary>
    internal static bool MayApprove(UserRole role, WorkflowAction action) =>
        TryGet(action, out var policy) && policy.AllowedApproverRoles.Contains(role);

    internal static WorkflowActionPolicy GetRequired(WorkflowAction action) =>
        TryGet(action, out var policy)
            ? policy
            : throw new InvalidOperationException("The requested action has no registered policy.");

    private static bool TryGet(WorkflowAction action, out WorkflowActionPolicy policy)
    {
        if (action is WorkflowAction.MarkVendorApproved)
        {
            policy = MarkVendorApproved;
            return true;
        }

        policy = null!;
        return false;
    }
}
