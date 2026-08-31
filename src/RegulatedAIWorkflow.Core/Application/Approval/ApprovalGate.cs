using RegulatedAIWorkflow.Core.Contracts.Approval;
using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Ports;

namespace RegulatedAIWorkflow.Core.Application.Approval;

/// <summary>
/// Re-checks every binding at the moment of use, because an approval that was valid when issued is not
/// necessarily valid now. An approval is never a caller-supplied name: only a stored record fetched by
/// (tenant, id) counts, and each way it can fail to bind is a distinct, auditable answer.
/// </summary>
public sealed class ApprovalGate(IApprovalRepository approvalRepository, TimeProvider timeProvider)
{
    /// <summary>Verifies a presented approval against the request it is being used for.</summary>
    public async Task<ApprovalDecision> VerifyAsync(
        WorkflowPrincipal requester,
        string vendorId,
        WorkflowAction requestedAction,
        string? approvalId,
        string evidenceSetHash,
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

        // The evidence moved after the signature, so the approver endorsed a different set of facts than
        // the one in front of us now.
        if (!string.Equals(approval.EvidenceSetHash, evidenceSetHash, StringComparison.Ordinal))
        {
            return new ApprovalDecision(ApprovalOutcome.EvidenceSuperseded, approval);
        }

        if (timeProvider.GetUtcNow() > approval.ExpiresAtUtc)
        {
            return new ApprovalDecision(ApprovalOutcome.Expired, approval);
        }

        if (string.Equals(approval.ApproverUserId, requester.UserId, StringComparison.Ordinal))
        {
            return new ApprovalDecision(ApprovalOutcome.SelfApproval, approval);
        }

        return new ApprovalDecision(ApprovalOutcome.Approved, approval);
    }
}
