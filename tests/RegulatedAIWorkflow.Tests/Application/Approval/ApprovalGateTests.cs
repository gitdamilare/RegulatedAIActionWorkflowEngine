using RegulatedAIWorkflow.Core.Application;
using RegulatedAIWorkflow.Core.Application.Approval;
using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Approval;
using RegulatedAIWorkflow.Infrastructure.Approval;

namespace RegulatedAIWorkflow.Tests.Application.Approval;

/// <summary>
/// Verifies every stored approval binding is enforced independently.
/// </summary>
public sealed class ApprovalGateTests
{
    /// <summary>Missing and unknown identifiers fail closed without a record.</summary>
    [Theory]
    [InlineData(null, ApprovalOutcome.Missing)]
    [InlineData("unknown", ApprovalOutcome.NotFound)]
    public async Task EvaluateAsync_MissingOrUnknownApproval_FailsClosed(
        string? approvalId,
        ApprovalOutcome expected)
    {
        var gate = new ApprovalGate(
            new InMemoryApprovalRepository(),
            new FixedTimeProvider(WorkflowTestHarness.ExpectedUtcNow),
            WorkflowActionCatalog.CreateDefault());

        var result = await gate.EvaluateAsync(
            Request(approvalId),
            CancellationToken.None);

        result.Outcome.ShouldBe(expected);
        result.Approval.ShouldBeNull();
    }

    /// <summary>A repository cannot escape the requested tenant or approval identity.</summary>
    [Theory]
    [InlineData("foreign-tenant", "approval")]
    [InlineData("northstar-bank", "different-id")]
    public async Task EvaluateAsync_LeakyRepositoryRecord_IsNormalizedToNotFound(
        string storedTenant,
        string storedApprovalId)
    {
        var gate = new ApprovalGate(
            new LeakyApprovalRepository(Record() with
            {
                TenantId = storedTenant,
                ApprovalId = storedApprovalId
            }),
            new FixedTimeProvider(WorkflowTestHarness.ExpectedUtcNow),
            WorkflowActionCatalog.CreateDefault());

        var result = await gate.EvaluateAsync(Request("approval"), CancellationToken.None);

        result.Outcome.ShouldBe(ApprovalOutcome.NotFound);
        result.Approval.ShouldBeNull();
    }

    /// <summary>Every approval binding mismatch produces its explicit rejection outcome.</summary>
    [Fact]
    public async Task EvaluateAsync_EachBindingMismatch_IsRejected()
    {
        var now = WorkflowTestHarness.ExpectedUtcNow;
        var scenarios = new (ApprovalRecord Record, ApprovalOutcome Expected)[]
        {
            (Record() with { Action = WorkflowAction.Unknown }, ApprovalOutcome.ActionMismatch),
            (Record() with { VendorId = "other-vendor" }, ApprovalOutcome.VendorMismatch),
            (Record() with { RiskPolicyVersion = "old-policy" }, ApprovalOutcome.PolicySuperseded),
            (Record() with { EvidenceSetHash = "old-hash" }, ApprovalOutcome.EvidenceSuperseded),
            (Record() with { IssuedAtUtc = now.AddMinutes(1) }, ApprovalOutcome.NotYetValid),
            (Record() with { ExpiresAtUtc = now }, ApprovalOutcome.Expired),
            (Record() with { ApproverUserId = "procurement-user" }, ApprovalOutcome.SelfApproval),
            (Record() with { ApproverRole = UserRole.ComplianceOfficer }, ApprovalOutcome.WrongRole)
        };

        foreach (var scenario in scenarios)
        {
            var repository = new InMemoryApprovalRepository();
            await repository.SaveAsync(scenario.Record, CancellationToken.None);
            var gate = new ApprovalGate(
                repository,
                new FixedTimeProvider(now),
                WorkflowActionCatalog.CreateDefault());

            var result = await gate.EvaluateAsync(Request("approval"), CancellationToken.None);

            result.Outcome.ShouldBe(scenario.Expected);
        }
    }

    /// <summary>A fully matching independent approval is accepted.</summary>
    [Fact]
    public async Task EvaluateAsync_AllBindingsMatch_ReturnsValid()
    {
        var repository = new InMemoryApprovalRepository();
        await repository.SaveAsync(Record(), CancellationToken.None);
        var gate = new ApprovalGate(
            repository,
            new FixedTimeProvider(WorkflowTestHarness.ExpectedUtcNow),
            WorkflowActionCatalog.CreateDefault());

        var result = await gate.EvaluateAsync(Request("approval"), CancellationToken.None);

        result.IsApproved.ShouldBeTrue();
        result.Approval.ShouldBe(Record());
    }

    private static ApprovalVerificationRequest Request(string? approvalId) =>
        new(
            WorkflowTestHarness.Principal(),
            "silverline-payments",
            WorkflowAction.MarkVendorApproved,
            "current-hash",
            "current-policy",
            approvalId);

    private static ApprovalRecord Record() =>
        new(
            "approval",
            "northstar-bank",
            "silverline-payments",
            WorkflowAction.MarkVendorApproved,
            "risk-approver",
            UserRole.RiskApprover,
            "current-hash",
            "current-policy",
            WorkflowTestHarness.ExpectedUtcNow.AddMinutes(-1),
            WorkflowTestHarness.ExpectedUtcNow.AddHours(1));
}
