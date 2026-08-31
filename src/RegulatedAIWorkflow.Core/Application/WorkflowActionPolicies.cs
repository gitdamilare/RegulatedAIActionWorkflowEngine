using System.Collections.Frozen;
using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Core.Application;

/// <summary>
/// What an action costs and who may ask for it. Requester and approver roles are disjoint sets, so
/// separation of duties is a property of the policy rather than a check somewhere downstream.
/// </summary>
internal sealed record WorkflowActionPolicy(
    RiskLevel BaselineRiskLevel,
    RiskReason BaselineRiskReason,
    RiskLevel? ApprovalRequiredAtOrAbove,
    IReadOnlyList<UserRole> AllowedRequesterRoles,
    IReadOnlyList<UserRole> AllowedApproverRoles);

/// <summary>
/// The only place an action is registered. Policy is server-owned: a caller names an action, never its
/// risk, its threshold, or the roles that may take it. Adding an action is one enum member and one entry
/// in this table.
/// </summary>
internal static class WorkflowActionPolicies
{
    private static readonly FrozenDictionary<WorkflowAction, WorkflowActionPolicy> Policies =
        new Dictionary<WorkflowAction, WorkflowActionPolicy>
        {
            // Irreversible and it authorizes payment-data processing, so the baseline is High and no
            // amount of clean evidence removes the need for a human. Evidence varies the reasons, the
            // gaps, and the citations; it never varies the level.
            [WorkflowAction.MarkVendorApproved] = new(
                RiskLevel.High,
                new RiskReason(
                    "ACTION_MARK_VENDOR_APPROVED_HIGH_RISK",
                    "Marking a vendor approved to process payment data is classified as a high-risk action."),
                ApprovalRequiredAtOrAbove: RiskLevel.High,
                [UserRole.ProcurementManager, UserRole.ComplianceOfficer],
                [UserRole.RiskApprover]),

            // Asking a vendor for missing evidence changes nothing a regulator would care about, so it
            // carries no baseline and never stops for approval. It exists to prove the registry is a
            // mechanism rather than one case with an interface around it.
            [WorkflowAction.RequestVendorEvidence] = new(
                RiskLevel.Low,
                new RiskReason(
                    "ACTION_REQUEST_VENDOR_EVIDENCE_LOW_RISK",
                    "Requesting outstanding evidence from a vendor is a reversible, low-risk action."),
                ApprovalRequiredAtOrAbove: null,
                [UserRole.ProcurementManager, UserRole.ComplianceOfficer, UserRole.Viewer],
                [])
        }.ToFrozenDictionary();

    internal static bool MayAttempt(UserRole role, WorkflowAction action) =>
        Policies.TryGetValue(action, out var policy) && policy.AllowedRequesterRoles.Contains(role);

    internal static bool MayApprove(UserRole role, WorkflowAction action) =>
        Policies.TryGetValue(action, out var policy) && policy.AllowedApproverRoles.Contains(role);

    internal static WorkflowActionPolicy GetRequired(WorkflowAction action) =>
        Policies.TryGetValue(action, out var policy)
            ? policy
            : throw new InvalidOperationException("The requested action has no registered policy.");
}
