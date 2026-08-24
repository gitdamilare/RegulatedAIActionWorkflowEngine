using RegulatedAIWorkflow.Core.Contracts.Audit;
using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Approval;
using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Risk;
using RegulatedAIWorkflow.Core.Ports;
using RegulatedAIWorkflow.Infrastructure.Approval;
using RegulatedAIWorkflow.Infrastructure.Evidence;
using RegulatedAIWorkflow.Tests.Application;

namespace RegulatedAIWorkflow.Tests.Required;

/// <summary>
/// Required test 2: the server-owned action policy classifies vendor approval as high risk and
/// requires a current, independent, evidence-bound approval before execution. Executor invocation
/// assertions are the proof that rejected approvals caused no regulated effect.
/// </summary>
public sealed class Required_2_ApprovalGateTests
{
    /// <summary>Verifies each policy-authorized requester reaches the real high-risk approval gate.</summary>
    [Theory]
    [InlineData(UserRole.ProcurementManager)]
    [InlineData(UserRole.ComplianceOfficer)]
    public async Task RunAsync_AuthorizedRoleWithHighRiskEvidence_ReturnsPendingApproval(UserRole role)
    {
        var harness = new WorkflowTestHarness();

        var result = await harness.CreateOrchestrator().RunAsync(
            WorkflowTestHarness.Principal(role),
            WorkflowTestHarness.Command());

        result.ActionStatus.ShouldBe(ActionStatus.BlockedPendingApproval);
        result.RiskLevel.ShouldBe(RiskLevel.High);
        result.RequiresApproval.ShouldBeTrue();
        result.Citations.Select(citation => citation.DocumentId).ShouldBe(
            ["northstar-policy-002", "northstar-silverline-contract"]);
        result.AuditEventIds.Count.ShouldBe(2);
        harness.ActionExecutor.Executions.ShouldBeEmpty();
        harness.AuditSink.Events.Select(item => item.Outcome).ShouldBe(
            [AuditOutcome.BlockedPendingApproval, AuditOutcome.BlockedPendingApproval]);
    }

    /// <summary>High-risk execution remains blocked when no approval is supplied.</summary>
    [Fact]
    public async Task RunAsync_MissingApproval_DoesNotCallExecutor()
    {
        var harness = new WorkflowTestHarness();

        var result = await harness.CreateOrchestrator().RunAsync(
            WorkflowTestHarness.Principal(),
            WorkflowTestHarness.Command());

        result.ActionStatus.ShouldBe(ActionStatus.BlockedPendingApproval);
        result.Recommendation.ShouldBe("Do not approve yet.");
        harness.ActionExecutor.Executions.ShouldBeEmpty();
        harness.AuditSink.Events.Select(item => item.EventType).ShouldBe(
            [AuditEventType.ActionAttempt, AuditEventType.WorkflowCompleted]);
        harness.AuditSink.Events.ShouldAllBe(item =>
            item.ReasonCodes.Contains(WorkflowAuditCodes.ApprovalMissing, StringComparer.Ordinal));
    }

    /// <summary>A valid independent approval permits execution without rewriting the assessment.</summary>
    [Fact]
    public async Task RunAsync_ValidIndependentApproval_ExecutesAndPreservesHighRiskAssessment()
    {
        var harness = new WorkflowTestHarness();
        var approval = await harness.IssueApprovalAsync();

        var result = await harness.CreateOrchestrator().RunAsync(
            WorkflowTestHarness.Principal(),
            WorkflowTestHarness.Command(approvalId: approval.ApprovalId));

        result.ActionStatus.ShouldBe(ActionStatus.Executed);
        result.RiskLevel.ShouldBe(RiskLevel.High);
        result.RequiresApproval.ShouldBeTrue();
        result.Recommendation.ShouldBe(
            "Proceeded under recorded approval. The assessment remains high and the evidence gaps listed below are still outstanding.");
        result.Reasons.ShouldNotBeEmpty();
        result.Citations.ShouldNotBeEmpty();
        result.MissingEvidence.ShouldNotBeEmpty();
        result.AuditEventIds.Count.ShouldBe(4);
        var execution = harness.ActionExecutor.Executions.ShouldHaveSingleItem();
        execution.TenantId.ShouldBe("northstar-bank");
        execution.VendorId.ShouldBe("silverline-payments");
        execution.RequestingUserId.ShouldBe("procurement-user");
        execution.Action.ShouldBe(WorkflowAction.MarkVendorApproved);
        var accepted = harness.AuditSink.Events
            .Where(item => item.Outcome == AuditOutcome.ApprovalAccepted)
            .ShouldHaveSingleItem();
        accepted.ApprovalId.ShouldBe(approval.ApprovalId);
        accepted.ApproverUserId.ShouldBe("risk-approver");
    }

    /// <summary>Tenant, vendor, and action mismatches all block the executor.</summary>
    [Fact]
    public async Task RunAsync_ScopeOrActionMismatch_DoesNotCallExecutor()
    {
        var harness = new WorkflowTestHarness();
        var issued = await harness.IssueApprovalAsync();
        var stored = await harness.ApprovalRepository.FindAsync(
            "northstar-bank",
            issued.ApprovalId!,
            CancellationToken.None);
        stored.ShouldNotBeNull();

        var actionRepository = new InMemoryApprovalRepository();
        await actionRepository.SaveAsync(
            stored with { Action = WorkflowAction.Unknown },
            CancellationToken.None);
        var vendorRepository = new InMemoryApprovalRepository();
        await vendorRepository.SaveAsync(
            stored with { VendorId = "other-vendor" },
            CancellationToken.None);
        var scenarios = new IApprovalRepository[]
        {
            new LeakyApprovalRepository(stored with { TenantId = "other-tenant" }),
            vendorRepository,
            actionRepository
        };

        foreach (var repository in scenarios)
        {
            var executor = new RecordingActionExecutor();
            var result = await harness.CreateOrchestrator(
                approvalRepository: repository,
                actionExecutor: executor).RunAsync(
                    WorkflowTestHarness.Principal(),
                    WorkflowTestHarness.Command(approvalId: issued.ApprovalId));

            result.ActionStatus.ShouldBe(ActionStatus.BlockedPendingApproval);
            executor.CallCount.ShouldBe(0);
        }
    }

    /// <summary>Self-approved and expired records cannot reach the executor.</summary>
    [Theory]
    [InlineData("self")]
    [InlineData("expired")]
    public async Task RunAsync_SeparationOrTimeFailure_DoesNotCallExecutor(string scenario)
    {
        var harness = new WorkflowTestHarness();
        var approver = scenario == "self"
            ? WorkflowTestHarness.Principal(UserRole.RiskApprover, userId: "same-user")
            : WorkflowTestHarness.Principal(UserRole.RiskApprover, userId: "risk-approver");
        var approval = await harness.IssueApprovalAsync(
            approver,
            validForHours: scenario == "expired" ? 1 : 24);
        if (scenario == "expired")
        {
            harness.TimeProvider.Advance(TimeSpan.FromHours(1));
        }

        var requester = WorkflowTestHarness.Principal(
            userId: scenario == "self" ? "same-user" : "procurement-user");
        var result = await harness.CreateOrchestrator().RunAsync(
            requester,
            WorkflowTestHarness.Command(approvalId: approval.ApprovalId));

        result.ActionStatus.ShouldBe(ActionStatus.BlockedPendingApproval);
        harness.ActionExecutor.Executions.ShouldBeEmpty();
        harness.AuditSink.Events.ShouldContain(item => item.Outcome == AuditOutcome.ApprovalRejected);
    }

    /// <summary>Document or typed-fact changes supersede the stored approval.</summary>
    [Theory]
    [InlineData("document")]
    [InlineData("fact")]
    public async Task RunAsync_ChangedEvidenceBinding_DoesNotCallExecutor(string scenario)
    {
        var harness = new WorkflowTestHarness();
        var approval = await harness.IssueApprovalAsync();
        IEvidenceRepository repository = scenario == "document"
            ? new ChangedDocumentEvidenceRepository(new InMemoryEvidenceRepository())
            : new ChangedFactEvidenceRepository(new InMemoryEvidenceRepository());

        var result = await harness.CreateOrchestrator(evidenceRepository: repository).RunAsync(
            WorkflowTestHarness.Principal(),
            WorkflowTestHarness.Command(approvalId: approval.ApprovalId));

        result.ActionStatus.ShouldBe(ActionStatus.BlockedPendingApproval);
        harness.ActionExecutor.Executions.ShouldBeEmpty();
        harness.AuditSink.Events.ShouldContain(item =>
            item.ReasonCodes.Contains(WorkflowAuditCodes.ApprovalEvidenceSuperseded, StringComparer.Ordinal));
    }

    /// <summary>Policy supersession is reported before the policy-bound hash mismatch.</summary>
    [Fact]
    public async Task RunAsync_ChangedPolicy_DoesNotCallExecutorAndReportsPolicySupersession()
    {
        var harness = new WorkflowTestHarness();
        var approval = await harness.IssueApprovalAsync();
        var evaluator = new StubRiskEvaluator(_ => WorkflowTestHarness.HighEvaluation(
            [new RiskCitationReference("northstar-policy-002")],
            policyVersion: "new-policy-version"));

        var result = await harness.CreateOrchestrator(riskEvaluator: evaluator).RunAsync(
            WorkflowTestHarness.Principal(),
            WorkflowTestHarness.Command(approvalId: approval.ApprovalId));

        result.ActionStatus.ShouldBe(ActionStatus.BlockedPendingApproval);
        harness.ActionExecutor.Executions.ShouldBeEmpty();
        harness.AuditSink.Events.ShouldContain(item =>
            item.ReasonCodes.Contains(WorkflowAuditCodes.ApprovalPolicySuperseded, StringComparer.Ordinal));
    }

    /// <summary>Complete evidence cannot lower the action-policy floor or bypass approval.</summary>
    [Fact]
    public async Task RunAsync_CompleteEvidence_RequiresApprovalBeforeExecution()
    {
        var harness = new WorkflowTestHarness();

        var blocked = await harness.CreateOrchestrator().RunAsync(
            WorkflowTestHarness.Principal(),
            WorkflowTestHarness.Command(vendorId: "lakeshore-analytics"));

        blocked.ActionStatus.ShouldBe(ActionStatus.BlockedPendingApproval);
        blocked.RiskLevel.ShouldBe(RiskLevel.High);
        blocked.RequiresApproval.ShouldBeTrue();
        blocked.Reasons.Select(reason => reason.Code).ShouldBe(
            ["ACTION_MARK_VENDOR_APPROVED_HIGH_RISK"]);
        harness.ActionExecutor.Executions.ShouldBeEmpty();

        var approval = await harness.IssueApprovalAsync(vendorId: "lakeshore-analytics");
        var executed = await harness.CreateOrchestrator().RunAsync(
            WorkflowTestHarness.Principal(),
            WorkflowTestHarness.Command(
                vendorId: "lakeshore-analytics",
                approvalId: approval.ApprovalId));

        executed.ActionStatus.ShouldBe(ActionStatus.Executed);
        executed.RiskLevel.ShouldBe(RiskLevel.High);
        executed.RequiresApproval.ShouldBeTrue();
        executed.Recommendation.ShouldBe(
            "Proceeded under recorded approval. The action remains classified as high risk.");
        harness.ActionExecutor.Executions.Count.ShouldBe(1);
    }

    private sealed class ChangedDocumentEvidenceRepository(IEvidenceRepository inner) : IEvidenceRepository
    {
        public async Task<EvidenceSearchResult> SearchEvidenceAsync(
            EvidenceQuery query,
            CancellationToken cancellationToken)
        {
            var evidence = await inner.SearchEvidenceAsync(query, cancellationToken);
            var documents = evidence.Documents.ToArray();
            documents[0] = documents[0] with
            {
                UntrustedSnippet = UntrustedText.FromExternalSource("changed after approval")
            };
            return new EvidenceSearchResult(documents, evidence.Facts);
        }
    }

    private sealed class ChangedFactEvidenceRepository(IEvidenceRepository inner) : IEvidenceRepository
    {
        public async Task<EvidenceSearchResult> SearchEvidenceAsync(
            EvidenceQuery query,
            CancellationToken cancellationToken)
        {
            var evidence = await inner.SearchEvidenceAsync(query, cancellationToken);
            var addedFact = new EvidenceFact(
                query.TenantId,
                query.VendorId,
                evidence.Documents[0].DocumentId,
                EvidenceFactType.Soc2Available);
            return new EvidenceSearchResult(evidence.Documents, [.. evidence.Facts, addedFact]);
        }
    }
}
