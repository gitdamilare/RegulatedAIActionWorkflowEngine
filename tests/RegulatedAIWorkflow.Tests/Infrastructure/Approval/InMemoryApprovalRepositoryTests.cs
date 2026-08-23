using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Approval;
using RegulatedAIWorkflow.Infrastructure.Approval;
using RegulatedAIWorkflow.Tests.Application;

namespace RegulatedAIWorkflow.Tests.Infrastructure.Approval;

/// <summary>
/// Verifies approval storage is scoped by exact tenant and approval identity.
/// </summary>
public sealed class InMemoryApprovalRepositoryTests
{
    /// <summary>The same approval ID remains isolated between tenants.</summary>
    [Fact]
    public async Task FindAsync_SharedApprovalId_KeepsTenantsIsolated()
    {
        var repository = new InMemoryApprovalRepository();
        var first = Record("tenant-a", "approver-a");
        var second = Record("tenant-b", "approver-b");
        await repository.SaveAsync(first, CancellationToken.None);
        await repository.SaveAsync(second, CancellationToken.None);

        var firstResult = await repository.FindAsync(
            "tenant-a",
            "shared-approval",
            CancellationToken.None);
        var secondResult = await repository.FindAsync(
            "tenant-b",
            "shared-approval",
            CancellationToken.None);

        firstResult.ShouldBe(first);
        secondResult.ShouldBe(second);
        (await repository.FindAsync(
            "Tenant-a",
            "shared-approval",
            CancellationToken.None)).ShouldBeNull();
    }

    private static ApprovalRecord Record(string tenantId, string approverUserId) =>
        new(
            "shared-approval",
            tenantId,
            "vendor",
            WorkflowAction.MarkVendorApproved,
            approverUserId,
            UserRole.RiskApprover,
            "hash",
            "policy",
            WorkflowTestHarness.ExpectedUtcNow,
            WorkflowTestHarness.ExpectedUtcNow.AddHours(1));
}
