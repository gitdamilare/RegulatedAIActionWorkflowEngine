using RegulatedAIWorkflow.Core.Application.Approval;
using RegulatedAIWorkflow.Core.Contracts.Approval;
using RegulatedAIWorkflow.Core.Contracts.Audit;
using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Evidence;

namespace RegulatedAIWorkflow.Tests.Application.Approval;

/// <summary>
/// Verifies approval issuance authorization, evidence binding, storage, and audit.
/// </summary>
public sealed class ApprovalIssuerTests
{
    /// <summary>Only a risk approver can retrieve evidence and issue an approval.</summary>
    [Theory]
    [InlineData(UserRole.Viewer)]
    [InlineData(UserRole.ProcurementManager)]
    [InlineData(UserRole.ComplianceOfficer)]
    public async Task IssueAsync_NonApproverRole_RetrievesNothingAndRejects(UserRole role)
    {
        var repository = new StubEvidenceRepository((_, _) =>
            Task.FromResult(WorkflowTestHarness.Evidence()));
        var harness = new WorkflowTestHarness();

        var result = await harness.CreateApprovalIssuer(evidenceRepository: repository).IssueAsync(
            WorkflowTestHarness.Principal(role),
            new IssueApprovalCommand(
                "silverline-payments",
                WorkflowAction.MarkVendorApproved,
                ValidForHours: 24));

        result.Outcome.ShouldBe(ApprovalIssueOutcome.ApproverRoleInsufficient);
        result.ApprovalId.ShouldBeNull();
        repository.CallCount.ShouldBe(0);
        var audit = harness.AuditSink.Events.ShouldHaveSingleItem();
        audit.Outcome.ShouldBe(AuditOutcome.ApprovalRejected);
        audit.ReasonCodes.ShouldContain(WorkflowAuditCodes.ApproverRoleInsufficient);
    }

    /// <summary>The stored record contains every required binding and server-owned timestamp.</summary>
    [Fact]
    public async Task IssueAsync_ValidRequest_StoresCompleteBindingAndAudits()
    {
        var harness = new WorkflowTestHarness();

        var result = await harness.IssueApprovalAsync();
        var stored = await harness.ApprovalRepository.FindAsync(
            "northstar-bank",
            result.ApprovalId!,
            CancellationToken.None);

        result.Outcome.ShouldBe(ApprovalIssueOutcome.Issued);
        stored.ShouldNotBeNull();
        stored.ApprovalId.ShouldBe(result.ApprovalId);
        stored.TenantId.ShouldBe("northstar-bank");
        stored.VendorId.ShouldBe("silverline-payments");
        stored.Action.ShouldBe(WorkflowAction.MarkVendorApproved);
        stored.ApproverUserId.ShouldBe("risk-approver");
        stored.ApproverRole.ShouldBe(UserRole.RiskApprover);
        stored.EvidenceSetHash.Length.ShouldBe(64);
        stored.RiskPolicyVersion.ShouldNotBeNullOrWhiteSpace();
        stored.IssuedAtUtc.ShouldBe(WorkflowTestHarness.ExpectedUtcNow);
        stored.ExpiresAtUtc.ShouldBe(WorkflowTestHarness.ExpectedUtcNow.AddHours(24));

        var audit = harness.AuditSink.Events.ShouldHaveSingleItem();
        audit.Outcome.ShouldBe(AuditOutcome.ApprovalRecorded);
        audit.ApprovalId.ShouldBe(stored.ApprovalId);
        audit.ApproverUserId.ShouldBe(stored.ApproverUserId);
    }

    /// <summary>Approval validity is bounded before evidence retrieval.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(169)]
    public async Task IssueAsync_InvalidValidity_RetrievesNothingAndRejects(int validForHours)
    {
        var repository = new StubEvidenceRepository((_, _) =>
            Task.FromResult(new EvidenceSearchResult([], [])));
        var harness = new WorkflowTestHarness();

        var result = await harness.CreateApprovalIssuer(evidenceRepository: repository).IssueAsync(
            WorkflowTestHarness.Principal(UserRole.RiskApprover),
            new IssueApprovalCommand(
                "silverline-payments",
                WorkflowAction.MarkVendorApproved,
                validForHours));

        result.Outcome.ShouldBe(ApprovalIssueOutcome.InvalidRequest);
        repository.CallCount.ShouldBe(0);
        harness.AuditSink.Events.ShouldHaveSingleItem().Outcome
            .ShouldBe(AuditOutcome.ApprovalRejected);
    }
}
