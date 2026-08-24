using RegulatedAIWorkflow.Core.Contracts.Workflow;

namespace RegulatedAIWorkflow.Core.Application;

/// <summary>
/// Defines which roles may attempt each regulated action.
/// </summary>
internal static class ActionAuthorizationPolicy
{
    internal static bool MayAttempt(UserRole role, WorkflowAction action) =>
        WorkflowActionPolicies.TryGet(action, out var policy) &&
        policy.AllowedRequesterRoles.Contains(role);

    internal static bool MayApprove(UserRole role, WorkflowAction action) =>
        WorkflowActionPolicies.TryGet(action, out var policy) &&
        policy.AllowedApproverRoles.Contains(role);
}
