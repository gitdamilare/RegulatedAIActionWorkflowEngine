using RegulatedAIWorkflow.Core.Contracts.Approval;

namespace RegulatedAIWorkflow.Core.Ports;

/// <summary>Stores and retrieves approvals within an explicit tenant scope.</summary>
public interface IApprovalRepository
{
    /// <summary>Persists a newly issued approval.</summary>
    Task SaveAsync(ApprovalRecord approval, CancellationToken cancellationToken);

    /// <summary>Finds an approval by id within one tenant, or null.</summary>
    Task<ApprovalRecord?> FindAsync(string tenantId, string approvalId, CancellationToken cancellationToken);
}
