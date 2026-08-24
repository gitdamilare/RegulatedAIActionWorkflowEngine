using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Core.Application;

/// <summary>Defines the small server-owned policy attached to a workflow action.</summary>
internal sealed record WorkflowActionPolicy(
    RiskLevel BaselineRiskLevel,
    RiskReason BaselineRiskReason,
    IReadOnlyList<UserRole> AllowedRequesterRoles,
    IReadOnlyList<UserRole> AllowedApproverRoles);

/// <summary>Resolves the policy for each supported workflow action.</summary>
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

    internal static bool TryGet(
        WorkflowAction action,
        out WorkflowActionPolicy policy)
    {
        if (action is WorkflowAction.MarkVendorApproved)
        {
            policy = MarkVendorApproved;
            return true;
        }

        policy = null!;
        return false;
    }

    internal static WorkflowActionPolicy GetRequired(WorkflowAction action) =>
        TryGet(action, out var policy)
            ? policy
            : throw new InvalidOperationException("The requested action has no registered policy.");
}
