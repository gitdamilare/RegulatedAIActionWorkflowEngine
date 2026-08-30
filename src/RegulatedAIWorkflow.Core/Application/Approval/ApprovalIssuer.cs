using RegulatedAIWorkflow.Core.Application.Workflow;
using RegulatedAIWorkflow.Core.Contracts.Approval;
using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Ports;

namespace RegulatedAIWorkflow.Core.Application.Approval;

/// <summary>
/// Records a human approval. It deliberately does not retrieve evidence or re-run risk: an approver acts
/// on the assessment they were already shown, and the gate re-checks every binding at use. Its two
/// refusals are named rather than collapsed, because a malformed request and a forbidden role are not
/// the same answer and must not become the same status code.
/// </summary>
public sealed class ApprovalIssuer(IApprovalRepository approvalRepository, TimeProvider timeProvider)
{
    /// <summary>Issues and stores an approval, or reports why it did not.</summary>
    public async Task<ApprovalIssueResult> IssueAsync(
        WorkflowPrincipal approver,
        string? vendorId,
        WorkflowAction requestedAction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approver);

        var validVendorId = WorkflowRequestValidator.SafeIdentifierOrNull(vendorId);
        if (validVendorId is null ||
            !WorkflowRequestValidator.IsValidIdentifier(approver.TenantId) ||
            !WorkflowRequestValidator.IsValidIdentifier(approver.UserId) ||
            !Enum.IsDefined(requestedAction) ||
            requestedAction is WorkflowAction.Unknown)
        {
            return new ApprovalIssueResult(ApprovalIssueOutcome.InvalidRequest, null);
        }

        if (!WorkflowActionPolicies.MayApprove(approver.Role, requestedAction))
        {
            return new ApprovalIssueResult(ApprovalIssueOutcome.ApproverRoleInsufficient, null);
        }

        var approval = new ApprovalRecord(
            $"apr-{Guid.CreateVersion7():N}",
            approver.TenantId,
            validVendorId,
            requestedAction,
            approver.UserId,
            approver.Role,
            timeProvider.GetUtcNow().ToUniversalTime());

        await approvalRepository.SaveAsync(approval, cancellationToken);
        return new ApprovalIssueResult(ApprovalIssueOutcome.Issued, approval);
    }
}
