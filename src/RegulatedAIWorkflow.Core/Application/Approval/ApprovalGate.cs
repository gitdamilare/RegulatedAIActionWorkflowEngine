using RegulatedAIWorkflow.Core.Contracts.Approval;
using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Ports;

namespace RegulatedAIWorkflow.Core.Application.Approval;

/// <summary>
/// Verifies that a presented approval was issued for exactly this request. Every check fails closed,
/// and a record the repository returns out of tenant scope is normalized to NotFound rather than trusted.
/// </summary>
public sealed class ApprovalGate(IApprovalRepository approvalRepository)
{
    /// <summary>Verifies an approval without performing the regulated action.</summary>
    public async Task<ApprovalDecision> VerifyAsync(
        WorkflowPrincipal requester,
        string vendorId,
        WorkflowAction requestedAction,
        string? approvalId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requester);

        if (string.IsNullOrWhiteSpace(approvalId))
        {
            return new ApprovalDecision(ApprovalOutcome.Missing, null);
        }

        var approval = await approvalRepository.FindAsync(requester.TenantId, approvalId, cancellationToken);

        if (approval is null ||
            !string.Equals(approval.TenantId, requester.TenantId, StringComparison.Ordinal) ||
            !string.Equals(approval.ApprovalId, approvalId, StringComparison.Ordinal))
        {
            return new ApprovalDecision(ApprovalOutcome.NotFound, null);
        }

        if (approval.Action != requestedAction ||
            !string.Equals(approval.VendorId, vendorId, StringComparison.Ordinal))
        {
            return new ApprovalDecision(ApprovalOutcome.Mismatch, approval);
        }

        if (string.Equals(approval.ApproverUserId, requester.UserId, StringComparison.Ordinal))
        {
            return new ApprovalDecision(ApprovalOutcome.SelfApproval, approval);
        }

        return new ApprovalDecision(ApprovalOutcome.Approved, approval);
    }
}
