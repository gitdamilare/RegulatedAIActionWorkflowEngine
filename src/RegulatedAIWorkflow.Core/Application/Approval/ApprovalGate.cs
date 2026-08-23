using RegulatedAIWorkflow.Core.Application.Workflow;
using RegulatedAIWorkflow.Core.Domain.Approval;
using RegulatedAIWorkflow.Core.Ports;

namespace RegulatedAIWorkflow.Core.Application.Approval;

/// <summary>
/// Verifies that a stored approval matches every current execution binding.
/// </summary>
public sealed class ApprovalGate(
    IApprovalRepository approvalRepository,
    TimeProvider timeProvider)
{
    /// <summary>Evaluates an approval without performing the regulated action.</summary>
    public async Task<ApprovalDecision> EvaluateAsync(
        ApprovalVerificationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ApprovalId))
        {
            return new ApprovalDecision(ApprovalOutcome.Missing, null);
        }

        var approval = await approvalRepository.FindAsync(
            request.Requester.TenantId,
            request.ApprovalId,
            cancellationToken);

        if (approval is null ||
            !string.Equals(approval.TenantId, request.Requester.TenantId, StringComparison.Ordinal) ||
            !string.Equals(approval.ApprovalId, request.ApprovalId, StringComparison.Ordinal))
        {
            return new ApprovalDecision(ApprovalOutcome.NotFound, null);
        }

        if (approval.Action != request.RequestedAction)
        {
            return new ApprovalDecision(ApprovalOutcome.ActionMismatch, approval);
        }

        if (!string.Equals(approval.VendorId, request.VendorId, StringComparison.Ordinal))
        {
            return new ApprovalDecision(ApprovalOutcome.VendorMismatch, approval);
        }

        if (!string.Equals(
                approval.RiskPolicyVersion,
                request.RiskPolicyVersion,
                StringComparison.Ordinal))
        {
            return new ApprovalDecision(ApprovalOutcome.PolicySuperseded, approval);
        }

        if (!string.Equals(
                approval.EvidenceSetHash,
                request.EvidenceSetHash,
                StringComparison.Ordinal))
        {
            return new ApprovalDecision(ApprovalOutcome.EvidenceSuperseded, approval);
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        if (now < approval.IssuedAtUtc)
        {
            return new ApprovalDecision(ApprovalOutcome.NotYetValid, approval);
        }

        if (now >= approval.ExpiresAtUtc)
        {
            return new ApprovalDecision(ApprovalOutcome.Expired, approval);
        }

        if (string.Equals(
                approval.ApproverUserId,
                request.Requester.UserId,
                StringComparison.Ordinal))
        {
            return new ApprovalDecision(ApprovalOutcome.SelfApproval, approval);
        }

        if (!ActionAuthorizationPolicy.MayApprove(approval.ApproverRole, approval.Action))
        {
            return new ApprovalDecision(ApprovalOutcome.WrongRole, approval);
        }

        return new ApprovalDecision(ApprovalOutcome.Valid, approval);
    }
}
