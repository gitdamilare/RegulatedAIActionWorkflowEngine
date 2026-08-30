using RegulatedAIWorkflow.Core.Contracts.Audit;
using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Tests;

/// <summary>
/// Brief bonus test: an unauthorized role cannot execute an action even with a valid tenant.
/// </summary>
public sealed class UnauthorizedRoleTests
{
    /// <summary>
    /// The retrieval count is the real assertion. Authorization runs before evidence is fetched, so a
    /// denied caller learns nothing about the vendor at all, not even that it exists.
    /// </summary>
    [Fact]
    public async Task RunAsync_ViewerRole_BlockedAndRetrievesNoEvidence()
    {
        var harness = new Harness();

        var result = await harness.Orchestrator().RunAsync(
            Harness.Principal(role: UserRole.Viewer),
            Harness.Command());

        result.ActionStatus.ShouldBe(ActionStatus.BlockedUnauthorized);
        result.RiskLevel.ShouldBe(RiskLevel.Unknown);
        result.Citations.ShouldBeEmpty();
        result.Reasons.ShouldBeEmpty();
        result.MissingEvidence.ShouldBeEmpty();

        harness.Evidence.CallCount.ShouldBe(0);
        harness.Executor.CallCount.ShouldBe(0);
        harness.Audit.Events.ShouldAllBe(auditEvent =>
            auditEvent.Outcome == AuditOutcome.BlockedUnauthorized &&
            auditEvent.ReasonCodes.Contains(WorkflowAuditCodes.RoleNotAuthorized));
    }

    /// <summary>Both roles the action names may request it; every other role is denied by default.</summary>
    [Theory]
    [InlineData(UserRole.ProcurementManager, true)]
    [InlineData(UserRole.ComplianceOfficer, true)]
    [InlineData(UserRole.RiskApprover, false)]
    [InlineData(UserRole.Viewer, false)]
    public async Task RunAsync_RequesterRole_DecidesWhetherTheRiskGateIsReachedAtAll(
        UserRole role,
        bool reachesGate)
    {
        var harness = new Harness();

        var result = await harness.Orchestrator().RunAsync(Harness.Principal(role: role), Harness.Command());

        result.ActionStatus.ShouldBe(
            reachesGate ? ActionStatus.BlockedPendingApproval : ActionStatus.BlockedUnauthorized);
        harness.Evidence.CallCount.ShouldBe(reachesGate ? 1 : 0);
        harness.Executor.CallCount.ShouldBe(0);
    }
}
