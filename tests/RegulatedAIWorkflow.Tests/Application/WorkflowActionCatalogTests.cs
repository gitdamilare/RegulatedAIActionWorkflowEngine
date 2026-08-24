using RegulatedAIWorkflow.Core.Application;
using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Tests.Application;

/// <summary>Verifies action policy is complete, deterministic, and fail closed.</summary>
public sealed class WorkflowActionCatalogTests
{
    /// <summary>The default catalog defines every usable enum action exactly once.</summary>
    [Fact]
    public void CreateDefault_RegistersCompleteMarkVendorApprovedPolicy()
    {
        var catalog = WorkflowActionCatalog.CreateDefault();
        var expectedActions = Enum.GetValues<WorkflowAction>()
            .Where(action => action is not WorkflowAction.Unknown)
            .ToArray();

        catalog.Version.ShouldBe("actions-2026.08.1");
        catalog.Definitions.Select(definition => definition.Action).ShouldBe(expectedActions);

        var definition = catalog.GetRequired(WorkflowAction.MarkVendorApproved);
        definition.BaselineRiskLevel.ShouldBe(RiskLevel.High);
        definition.BaselineRiskReason.Code.ShouldBe(
            "ACTION_MARK_VENDOR_APPROVED_HIGH_RISK");
        definition.AllowedRequesterRoles.ShouldBe(
            [UserRole.ProcurementManager, UserRole.ComplianceOfficer],
            ignoreOrder: true);
        definition.AllowedApproverRoles.ShouldBe([UserRole.RiskApprover]);
    }

    /// <summary>Unknown and undefined action identifiers are denied by every catalog operation.</summary>
    [Theory]
    [InlineData(WorkflowAction.Unknown)]
    [InlineData((WorkflowAction)999)]
    public void Authorization_UnrecognizedAction_FailsClosed(WorkflowAction action)
    {
        var catalog = WorkflowActionCatalog.CreateDefault();

        catalog.TryGet(action, out _).ShouldBeFalse();
        catalog.MayAttempt(UserRole.ProcurementManager, action).ShouldBeFalse();
        catalog.MayApprove(UserRole.RiskApprover, action).ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => catalog.GetRequired(action));
    }

    /// <summary>Duplicate action definitions cannot create ambiguous policy.</summary>
    [Fact]
    public void Constructor_DuplicateActionDefinitions_ThrowsArgumentException()
    {
        var definition = WorkflowActionCatalog.CreateDefault().Definitions.ShouldHaveSingleItem();

        Should.Throw<ArgumentException>(() =>
            new WorkflowActionCatalog("actions-duplicate", [definition, definition]));
    }
}
