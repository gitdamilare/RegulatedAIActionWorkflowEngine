using System.Collections.Concurrent;
using RegulatedAIWorkflow.Core.Contracts.Approval;
using RegulatedAIWorkflow.Core.Ports;

namespace RegulatedAIWorkflow.Infrastructure.Approval;

/// <summary>Tenant-scoped approval storage. The tenant is part of the key, so a lookup cannot cross tenants.</summary>
public sealed class InMemoryApprovalRepository : IApprovalRepository
{
    private readonly ConcurrentDictionary<ApprovalKey, ApprovalRecord> approvals = new();

    /// <inheritdoc />
    public Task SaveAsync(ApprovalRecord approval, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approval);
        cancellationToken.ThrowIfCancellationRequested();
        approvals[new ApprovalKey(approval.TenantId, approval.ApprovalId)] = approval;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ApprovalRecord?> FindAsync(
        string tenantId,
        string approvalId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        approvals.TryGetValue(new ApprovalKey(tenantId, approvalId), out var approval);
        return Task.FromResult(approval);
    }

    private readonly record struct ApprovalKey(string TenantId, string ApprovalId);
}
