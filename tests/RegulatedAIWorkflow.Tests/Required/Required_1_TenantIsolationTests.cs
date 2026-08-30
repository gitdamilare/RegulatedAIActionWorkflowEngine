using System.Text.Json;
using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Infrastructure.Evidence;

namespace RegulatedAIWorkflow.Tests.Required;

/// <summary>
/// Brief required test 1: tenant-a cannot access tenant-b evidence.
/// </summary>
public sealed class Required_1_TenantIsolationTests
{
    /// <summary>
    /// Both tenants hold a vendor with the same id. Sharing the id is what makes the isolation real:
    /// a filter bug would return the other tenant's corpus rather than an empty set.
    /// </summary>
    [Fact]
    public async Task SearchEvidenceAsync_SharedVendorIdAcrossTenants_ReturnsOnlyCallerTenantDocuments()
    {
        var repository = new InMemoryEvidenceRepository();

        var tenantA = await repository.SearchEvidenceAsync(
            new EvidenceQuery(Harness.TenantA, Harness.Vendor),
            CancellationToken.None);
        var tenantB = await repository.SearchEvidenceAsync(
            new EvidenceQuery(Harness.TenantB, Harness.Vendor),
            CancellationToken.None);

        tenantA.ShouldNotBeEmpty();
        tenantB.ShouldNotBeEmpty();
        tenantA.ShouldAllBe(document => document.TenantId == Harness.TenantA);
        tenantB.ShouldAllBe(document => document.TenantId == Harness.TenantB);

        var tenantADocumentIds = tenantA.Select(document => document.DocumentId).ToArray();
        var tenantBDocumentIds = tenantB.Select(document => document.DocumentId).ToArray();
        tenantADocumentIds.Intersect(tenantBDocumentIds, StringComparer.Ordinal).ShouldBeEmpty();
    }

    /// <summary>
    /// A vendor that exists only in tenant A must look exactly like a vendor that exists nowhere when
    /// tenant B asks for it. Any difference in status, risk, reasons, or body is an existence oracle.
    /// </summary>
    [Fact]
    public async Task RunAsync_VendorOnlyInAnotherTenant_ReturnsSameDenialAsUnknownVendor()
    {
        var harness = new Harness();
        var orchestrator = harness.Orchestrator();

        var crossTenant = await orchestrator.RunAsync(
            Harness.Principal(Harness.TenantB),
            Harness.Command(Harness.TenantAOnlyVendor));
        var neverExisted = await orchestrator.RunAsync(
            Harness.Principal(Harness.TenantB),
            Harness.Command("no-such-vendor-at-all"));

        crossTenant.ActionStatus.ShouldBe(ActionStatus.DeniedUnknownSubject);
        Fingerprint(crossTenant).ShouldBe(Fingerprint(neverExisted));

        crossTenant.Citations.ShouldBeEmpty();
        crossTenant.MissingEvidence.ShouldBeEmpty();
        crossTenant.RequiresApproval.ShouldBeFalse();
        harness.Executor.CallCount.ShouldBe(0);
    }

    /// <summary>Everything a caller can observe, minus the two ids that legitimately differ per run.</summary>
    private static string Fingerprint(WorkflowRunResult result) =>
        JsonSerializer.Serialize(result with { WorkflowId = Guid.Empty, AuditEventIds = [] });
}
