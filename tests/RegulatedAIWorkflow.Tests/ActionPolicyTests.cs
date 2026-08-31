using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Tests;

/// <summary>
/// Two registered actions with genuinely different policy. Only the claims nothing else in the suite makes
/// are here: that evidence moves the level when an action has no baseline of its own, that the approval
/// threshold belongs to the action rather than the level, and that requester roles are per action.
/// </summary>
public sealed class ActionPolicyTests
{
    /// <summary>All three levels reachable through the orchestrator, not only in the rule unit tests.</summary>
    [Theory]
    [InlineData(Harness.TenantA, Harness.LowRiskVendor, RiskLevel.Low)]
    [InlineData(Harness.TenantB, Harness.Vendor, RiskLevel.Medium)]
    [InlineData(Harness.TenantA, Harness.Vendor, RiskLevel.High)]
    public async Task RunAsync_LowConsequenceAction_LevelFollowsTheEvidence(
        string tenantId,
        string vendorId,
        RiskLevel expected)
    {
        var harness = new Harness();

        var result = await harness.Orchestrator().RunAsync(
            Harness.Principal(tenantId: tenantId),
            Harness.Command(vendorId: vendorId, action: WorkflowAction.RequestVendorEvidence));

        result.RiskLevel.ShouldBe(expected);
    }

    /// <summary>
    /// A high assessment does not imply a human. The threshold belongs to the action, so the reversible
    /// action proceeds on exactly the evidence that blocks `markVendorApproved` in Required_2.
    /// </summary>
    [Fact]
    public async Task RunAsync_LowConsequenceActionAtHighRisk_ExecutesWithoutApproval()
    {
        var harness = new Harness();

        var result = await harness.Orchestrator().RunAsync(
            Harness.Principal(),
            Harness.Command(action: WorkflowAction.RequestVendorEvidence));

        result.RiskLevel.ShouldBe(RiskLevel.High);
        result.RequiresApproval.ShouldBeFalse();
        result.ActionStatus.ShouldBe(ActionStatus.Executed);
        harness.Executor.CallCount.ShouldBe(1);
        harness.Executor.Requests[0].RequestedAction.ShouldBe(WorkflowAction.RequestVendorEvidence);
    }

    /// <summary>Requester roles are per action: the Viewer refused `markVendorApproved` is permitted here.</summary>
    [Fact]
    public async Task RunAsync_ViewerOnTheLowConsequenceAction_IsPermitted()
    {
        var harness = new Harness();

        var result = await harness.Orchestrator().RunAsync(
            Harness.Principal(role: UserRole.Viewer),
            Harness.Command(action: WorkflowAction.RequestVendorEvidence));

        result.ActionStatus.ShouldBe(ActionStatus.Executed);
        harness.Evidence.CallCount.ShouldBe(1);
    }
}
