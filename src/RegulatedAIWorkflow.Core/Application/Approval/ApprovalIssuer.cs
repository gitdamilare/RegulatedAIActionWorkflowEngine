using RegulatedAIWorkflow.Core.Application.Workflow;
using RegulatedAIWorkflow.Core.Contracts.Approval;
using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Ports;

namespace RegulatedAIWorkflow.Core.Application.Approval;

/// <summary>
/// Records a human approval, bound to the evidence that was on the table when it was granted.
/// <para>
/// It retrieves that evidence itself rather than accepting a hash from the caller. A caller-supplied
/// binding would let someone approve against a set of facts nobody ever saw, which is the one way to make
/// an evidence binding worse than no binding at all.
/// </para>
/// <para>
/// Its refusals are named rather than collapsed, because a malformed request and a forbidden role are not
/// the same answer and must not become the same status code. A vendor with no evidence in this tenant is
/// reported the same way as a malformed one, so issuing an approval cannot be used to discover whether a
/// vendor exists in somebody else's tenant.
/// </para>
/// </summary>
public sealed class ApprovalIssuer(
    IApprovalRepository approvalRepository,
    IEvidenceRepository evidenceRepository,
    TimeProvider timeProvider)
{
    /// <summary>
    /// How long an approval authorizes. A window this system can actually enforce, against an injected
    /// clock the tests can move, rather than a field nothing reads.
    /// </summary>
    private static readonly TimeSpan ValidityWindow = TimeSpan.FromHours(24);

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

        var query = new EvidenceQuery(approver.TenantId, validVendorId);
        var documents = await evidenceRepository.SearchEvidenceAsync(query, cancellationToken);

        // The same distrust the orchestrator applies, against the same definition of scope.
        if (documents.Any(document => !query.Covers(document)))
        {
            throw new InvalidOperationException("The evidence repository returned out-of-scope content.");
        }

        if (documents.Count == 0)
        {
            return new ApprovalIssueResult(ApprovalIssueOutcome.InvalidRequest, null);
        }

        var issuedAt = timeProvider.GetUtcNow().ToUniversalTime();
        var approval = new ApprovalRecord(
            $"apr-{Guid.CreateVersion7():N}",
            approver.TenantId,
            validVendorId,
            requestedAction,
            approver.UserId,
            approver.Role,
            EvidenceSetHash.Compute(query, requestedAction, documents),
            issuedAt,
            issuedAt + ValidityWindow);

        await approvalRepository.SaveAsync(approval, cancellationToken);
        return new ApprovalIssueResult(ApprovalIssueOutcome.Issued, approval);
    }
}
