using RegulatedAIWorkflow.Core.Contracts.Workflow;

namespace RegulatedAIWorkflow.Core.Application;

/// <summary>
/// Defines which roles may attempt each regulated action.
/// </summary>
internal static class ActionAuthorizationPolicy
{
    internal static bool MayAttempt(UserRole role, WorkflowAction action) =>
        (role is UserRole.ProcurementManager or UserRole.ComplianceOfficer) &&
        action is WorkflowAction.MarkVendorApproved;

    internal static bool MayApprove(UserRole role, WorkflowAction action) =>
        role is UserRole.RiskApprover &&
        action is WorkflowAction.MarkVendorApproved;
}
