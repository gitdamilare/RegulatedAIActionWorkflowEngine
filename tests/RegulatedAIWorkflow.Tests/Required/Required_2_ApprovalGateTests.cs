using RegulatedAIWorkflow.Core.Contracts.Audit;
using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Tests.Required;

/// <summary>
/// Brief required test 2: a high-risk markVendorApproved is blocked without approval. Throughout this
/// file the load-bearing assertion is executor call count, not the returned status: a status can be
/// wrong without harm, but a call to the executor is a regulated effect that already happened.
/// </summary>
public sealed class Required_2_ApprovalGateTests
{
    [Fact]
    public async Task RunAsync_HighRiskWithoutApproval_BlocksAndNeverCallsExecutor()
    {
        var harness = new Harness();

        var result = await harness.Orchestrator().RunAsync(Harness.Principal(), Harness.Command());

        result.ActionStatus.ShouldBe(ActionStatus.BlockedPendingApproval);
        result.RiskLevel.ShouldBe(RiskLevel.High);
        result.RequiresApproval.ShouldBeTrue();
        result.Reasons.ShouldNotBeEmpty();
        result.Citations.ShouldNotBeEmpty();
        result.MissingEvidence.ShouldNotBeEmpty();

        harness.Executor.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_MatchingRecordedApproval_CallsExecutorExactlyOnce()
    {
        var harness = new Harness();
        var approval = await harness.IssueApprovalAsync();

        var blocked = await harness.Orchestrator().RunAsync(Harness.Principal(), Harness.Command());
        var executed = await harness.Orchestrator().RunAsync(
            Harness.Principal(),
            Harness.Command(approvalId: approval.ApprovalId));

        executed.ActionStatus.ShouldBe(ActionStatus.Executed);
        harness.Executor.CallCount.ShouldBe(1);
        harness.Executor.Requests[0].VendorId.ShouldBe(Harness.Vendor);
        harness.Executor.Requests[0].ActorUserId.ShouldBe(Harness.Requester);

        // Approval authorizes the effect. It does not rewrite the assessment or clear the gaps.
        executed.RiskLevel.ShouldBe(RiskLevel.High);
        executed.RequiresApproval.ShouldBeTrue();
        executed.Reasons.ShouldBe(blocked.Reasons);
        executed.MissingEvidence.ShouldBe(blocked.MissingEvidence);
    }

    /// <summary>
    /// An approval authorizes exactly one tenant, vendor, and action, and never the person who asked
    /// for it. Every way of missing that binding must fail closed.
    /// </summary>
    [Theory]
    [InlineData("unknown-id", WorkflowAuditCodes.ApprovalNotFound)]
    [InlineData("wrong-vendor", WorkflowAuditCodes.ApprovalMismatch)]
    [InlineData("wrong-tenant", WorkflowAuditCodes.ApprovalNotFound)]
    [InlineData("self-approval", WorkflowAuditCodes.ApprovalSelfApproval)]
    [InlineData("none", WorkflowAuditCodes.ApprovalMissing)]
    public async Task RunAsync_ApprovalThatDoesNotBind_DoesNotCallExecutor(string scenario, string expectedCode)
    {
        var harness = new Harness();

        var (principal, approvalId) = scenario switch
        {
            "unknown-id" => (Harness.Principal(), "apr-does-not-exist"),
            "wrong-vendor" => (
                Harness.Principal(),
                (await harness.IssueApprovalAsync(vendorId: Harness.TenantAOnlyVendor)).ApprovalId),
            "wrong-tenant" => (
                Harness.Principal(),
                (await harness.IssueApprovalAsync(tenantId: Harness.TenantB)).ApprovalId),
            "self-approval" => (
                Harness.Principal(userId: Harness.Approver),
                (await harness.IssueApprovalAsync()).ApprovalId),
            _ => (Harness.Principal(), (string?)null)
        };

        var result = await harness.Orchestrator().RunAsync(
            principal,
            Harness.Command(approvalId: approvalId));

        result.ActionStatus.ShouldBe(ActionStatus.BlockedPendingApproval);
        harness.Executor.CallCount.ShouldBe(0);
        harness.Audit.Events.ShouldAllBe(auditEvent => auditEvent.ReasonCodes.Contains(expectedCode));
    }
}
