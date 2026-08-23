using RegulatedAIWorkflow.Core.Domain.Approval;

namespace RegulatedAIWorkflow.Core.Ports;

/// <summary>
/// Stores and retrieves approvals within an explicit tenant scope.
/// </summary>
public interface IApprovalRepository
{
    Task SaveAsync(ApprovalRecord approval, CancellationToken cancellationToken);

    Task<ApprovalRecord?> FindAsync(
        string tenantId,
        string approvalId,
        CancellationToken cancellationToken);
}
