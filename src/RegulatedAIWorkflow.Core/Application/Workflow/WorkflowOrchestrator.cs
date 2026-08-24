using RegulatedAIWorkflow.Core.Application.Approval;
using RegulatedAIWorkflow.Core.Contracts.Audit;
using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Approval;
using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Execution;
using RegulatedAIWorkflow.Core.Domain.Risk;
using RegulatedAIWorkflow.Core.Ports;

namespace RegulatedAIWorkflow.Core.Application.Workflow;

/// <summary>
/// Runs the workflow through its trust, policy, approval, audit, and execution gates in order.
/// </summary>
public sealed class WorkflowOrchestrator(
    IEvidenceRepository evidenceRepository,
    IRiskEvaluator riskEvaluator,
    ApprovalGate approvalGate,
    IAuditSink auditSink,
    IActionExecutor actionExecutor,
    TimeProvider timeProvider)
{
    private const string ApprovedExecutionRecommendation =
        "Proceeded under recorded approval. The assessment remains high and the evidence gaps listed below are still outstanding.";
    private const string ApprovedInherentActionRiskRecommendation =
        "Proceeded under recorded approval. The action remains classified as high risk.";
    private const string UnknownSubjectRecommendation =
        "No such subject in this tenant.";

    /// <summary>
    /// Validates, authorizes, assesses, verifies, audits, and conditionally executes a workflow request.
    /// </summary>
    public async Task<WorkflowRunResult> RunAsync(
        WorkflowPrincipal? principal,
        WorkflowCommand? command,
        CancellationToken cancellationToken = default)
    {
        var workflowId = Guid.CreateVersion7();
        var auditContext = new WorkflowAuditContext(workflowId)
        {
            TenantId = WorkflowRequestValidator.SafeIdentifierOrNull(principal?.TenantId),
            ActorUserId = WorkflowRequestValidator.SafeIdentifierOrNull(principal?.UserId),
            ActorRole = ValidateUserRole(principal),
            VendorId = WorkflowRequestValidator.SafeIdentifierOrNull(command?.VendorId),
            RequestedAction = ValidateWorkflowCommand(command),
            ApprovalId = WorkflowRequestValidator.SafeIdentifierOrNull(command?.ApprovalId)
        };

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Stage 1: Validate and reduce the request to safe identity, scope, and action data.
            var validated = WorkflowRequestValidator.Validate(principal, command);
            if (validated is null)
            {
                auditContext.ReasonCodes = [WorkflowAuditCodes.InvalidRequest];
                return await CompleteAsync(
                    auditContext,
                    CreateUnknownResult(workflowId, ActionStatus.BlockedInvalidRequest),
                    AuditOutcome.InvalidRequest);
            }

            auditContext.TenantId = validated.TenantId;
            auditContext.ActorUserId = validated.UserId;
            auditContext.ActorRole = validated.Role;
            auditContext.VendorId = validated.VendorId;
            auditContext.RequestedAction = validated.RequestedAction;
            auditContext.ApprovalId = validated.ApprovalId;

            // Stage 2: Authorize before any evidence can be retrieved or evaluated.
            if (!ActionAuthorizationPolicy.MayAttempt(validated.Role, validated.RequestedAction))
            {
                auditContext.ReasonCodes = [WorkflowAuditCodes.RoleNotAuthorized];
                return await CompleteAsync(
                    auditContext,
                    CreateUnknownResult(workflowId, ActionStatus.BlockedUnauthorized),
                    AuditOutcome.BlockedUnauthorized);
            }

            // Stage 3: Retrieve evidence using only the validated tenant and vendor scope.
            var retrieved = await evidenceRepository.SearchEvidenceAsync(
                new EvidenceQuery(validated.TenantId, validated.VendorId),
                cancellationToken);

            // Stage 4: Re-scope repository output at the Core trust boundary.
            var scoped = EvidenceSecurity.EnforceScope(
                retrieved,
                validated.TenantId,
                validated.VendorId);

            if (scoped.HadOutOfScopeContent)
            {
                auditContext.ReasonCodes = [WorkflowAuditCodes.EvidenceScopeViolation];
                return await CompleteAsync(
                    auditContext,
                    CreateUnknownResult(workflowId, ActionStatus.BlockedEvidenceUnavailable),
                    AuditOutcome.BlockedEvidenceUnavailable);
            }

            if (scoped.Evidence.Documents.Count == 0)
            {
                auditContext.ReasonCodes = [WorkflowAuditCodes.UnknownSubject];
                return await CompleteAsync(
                    auditContext,
                    CreateUnknownSubjectResult(workflowId),
                    AuditOutcome.DeniedUnknownSubject);
            }

            // Stage 5: Evaluate only retained, typed facts; evidence prose never reaches policy.
            var evaluation = riskEvaluator.EvaluateRisk(new RiskEvaluationInput(
                validated.RequestedAction,
                scoped.Evidence.Facts,
                HasScopedEvidence: true));

            if (!Enum.IsDefined(evaluation.RiskLevel) || evaluation.RiskLevel is RiskLevel.Unknown)
            {
                throw new InvalidOperationException("The risk evaluator returned an invalid risk level.");
            }

            // Stage 6: Verify every citation against retained documents and fact provenance.
            if (!VerifiedCitationResolver.TryResolve(
                    evaluation.CitationReferences,
                    scoped.Evidence,
                    out var citations))
            {
                auditContext.ReasonCodes = [WorkflowAuditCodes.CitationVerificationFailed];
                return await CompleteAsync(
                    auditContext,
                    CreateUnknownResult(workflowId, ActionStatus.BlockedEvidenceUnavailable),
                    AuditOutcome.BlockedEvidenceUnavailable);
            }

            auditContext.RiskLevel = evaluation.RiskLevel;
            auditContext.PolicyVersion = evaluation.PolicyVersion;
            auditContext.ReferencedDocumentIds = citations.Select(citation => citation.DocumentId).ToArray();
            auditContext.ReasonCodes = evaluation.Reasons.Select(reason => reason.Code).ToArray();
            auditContext.MissingEvidenceCodes = evaluation.MissingEvidence.Select(item => item.Code).ToArray();

            if (evaluation.EvidenceIsAmbiguous)
            {
                auditContext.ReasonCodes = [.. auditContext.ReasonCodes, WorkflowAuditCodes.EvidenceGateFailed];
                return await CompleteAsync(
                    auditContext,
                    CreateAssessedResult(
                        workflowId,
                        evaluation,
                        [],
                        ActionStatus.BlockedEvidenceUnavailable),
                    AuditOutcome.BlockedEvidenceUnavailable);
            }

            if (evaluation.RequiresApproval)
            {
                var evidenceSetHash = CanonicalEvidenceHasher.Compute(
                    validated.TenantId,
                    validated.VendorId,
                    scoped.Evidence,
                    evaluation.PolicyVersion);
                var approval = await approvalGate.EvaluateAsync(
                    new ApprovalVerificationRequest(
                        new WorkflowPrincipal(
                            validated.TenantId,
                            validated.UserId,
                            validated.Role),
                        validated.VendorId,
                        validated.RequestedAction,
                        evidenceSetHash,
                        evaluation.PolicyVersion,
                        validated.ApprovalId),
                    cancellationToken);

                auditContext.ApproverUserId = approval.Approval?.ApproverUserId;
                if (!approval.IsApproved)
                {
                    auditContext.ReasonCodes =
                    [
                        .. auditContext.ReasonCodes,
                        ApprovalReasonCode(approval.Outcome)
                    ];

                    if (validated.ApprovalId is not null)
                    {
                        await WriteAuditAsync(
                            auditContext,
                            AuditEventType.ApprovalDecision,
                            AuditOutcome.ApprovalRejected);
                    }

                    return await CompleteAsync(
                        auditContext,
                        CreateAssessedResult(
                            workflowId,
                            evaluation,
                            citations,
                            ActionStatus.BlockedPendingApproval),
                        AuditOutcome.BlockedPendingApproval);
                }

                await WriteAuditAsync(
                    auditContext,
                    AuditEventType.ApprovalDecision,
                    AuditOutcome.ApprovalAccepted);
            }

            // Stage 7: Persist authorization before any regulated side effect can begin.
            await WriteAuditAsync(
                auditContext,
                AuditEventType.ActionAttempt,
                AuditOutcome.AuthorizedForExecution);

            // Stage 8: Execute only after every applicable gate and mandatory audit write passed.
            var execution = await actionExecutor.ExecuteAsync(
                new ActionExecutionRequest(
                    workflowId,
                    validated.TenantId,
                    validated.VendorId,
                    validated.UserId,
                    validated.RequestedAction),
                cancellationToken);
            if (!execution.Succeeded)
            {
                throw new InvalidOperationException("The action executor reported failure.");
            }

            await WriteAuditAsync(
                auditContext,
                AuditEventType.ActionExecution,
                AuditOutcome.Executed);

            return await CompleteExecutedAsync(
                auditContext,
                CreateAssessedResult(
                    workflowId,
                    evaluation,
                    citations,
                    ActionStatus.Executed));
        }
        catch
        {
            await WriteAuditAsync(
                auditContext,
                AuditEventType.WorkflowCompleted,
                AuditOutcome.Failed);
            throw;
        }
    }

    private static WorkflowRunResult CreateUnknownResult(
        Guid workflowId,
        ActionStatus actionStatus) =>
        new(
            workflowId,
            RiskLevel.Unknown,
            string.Empty,
            [],
            [],
            [],
            RequiresApproval: false,
            actionStatus,
            AuditEventIds: []);

    private static WorkflowRunResult CreateUnknownSubjectResult(Guid workflowId) =>
        new(
            workflowId,
            RiskLevel.Unknown,
            UnknownSubjectRecommendation,
            [new RiskReason(WorkflowAuditCodes.UnknownSubject, UnknownSubjectRecommendation)],
            [],
            [],
            RequiresApproval: false,
            ActionStatus.DeniedUnknownSubject,
            AuditEventIds: []);

    private static WorkflowRunResult CreateAssessedResult(
        Guid workflowId,
        RiskEvaluation evaluation,
        IReadOnlyList<Citation> citations,
        ActionStatus actionStatus) =>
        new(
            workflowId,
            evaluation.RiskLevel,
            RecommendationFor(evaluation, actionStatus),
            evaluation.Reasons,
            citations,
            evaluation.MissingEvidence,
            evaluation.RequiresApproval,
            actionStatus,
            AuditEventIds: []);

    private static string RecommendationFor(
        RiskEvaluation evaluation,
        ActionStatus actionStatus) =>
        actionStatus is ActionStatus.Executed && evaluation.RequiresApproval
            ? evaluation.MissingEvidence.Count > 0
                ? ApprovedExecutionRecommendation
                : ApprovedInherentActionRiskRecommendation
            : evaluation.Recommendation;

    private async Task<WorkflowRunResult> CompleteAsync(
        WorkflowAuditContext auditContext,
        WorkflowRunResult result,
        AuditOutcome outcome)
    {
        // Stage 7: Persist the attempt and terminal outcome before exposing a result.
        await WriteAuditAsync(auditContext, AuditEventType.ActionAttempt, outcome);
        await WriteAuditAsync(auditContext, AuditEventType.WorkflowCompleted, outcome);

        // Stage 8: Return only the identifiers of audit events that were written successfully.
        return result with { AuditEventIds = auditContext.EventIds.ToArray() };
    }

    private async Task<WorkflowRunResult> CompleteExecutedAsync(
        WorkflowAuditContext auditContext,
        WorkflowRunResult result)
    {
        await WriteAuditAsync(
            auditContext,
            AuditEventType.WorkflowCompleted,
            AuditOutcome.Executed);
        return result with { AuditEventIds = auditContext.EventIds.ToArray() };
    }

    private async Task WriteAuditAsync(
        WorkflowAuditContext auditContext,
        AuditEventType eventType,
        AuditOutcome outcome)
    {
        var eventId = Guid.CreateVersion7();
        var auditEvent = new AuditEvent(
            eventId,
            auditContext.WorkflowId,
            timeProvider.GetUtcNow().ToUniversalTime(),
            auditContext.TenantId,
            auditContext.ActorUserId,
            auditContext.ActorRole,
            auditContext.VendorId,
            eventType,
            auditContext.RequestedAction,
            auditContext.RiskLevel,
            outcome,
            auditContext.ReferencedDocumentIds,
            auditContext.ReasonCodes,
            auditContext.MissingEvidenceCodes,
            auditContext.PolicyVersion,
            auditContext.ApprovalId,
            auditContext.ApproverUserId);

        await auditSink.WriteAuditEventAsync(auditEvent, CancellationToken.None);
        auditContext.EventIds.Add(eventId);
    }

    private static WorkflowAction ValidateWorkflowCommand(WorkflowCommand? command) =>
        command is not null && Enum.IsDefined(command.RequestedAction)
            ? command.RequestedAction
            : WorkflowAction.Unknown;

    private static UserRole ValidateUserRole(WorkflowPrincipal? principal) =>
        principal is not null && Enum.IsDefined(principal.Role)
            ? principal.Role
            : UserRole.Unknown;

    private static string ApprovalReasonCode(ApprovalOutcome outcome) => outcome switch
    {
        ApprovalOutcome.Missing => WorkflowAuditCodes.ApprovalMissing,
        ApprovalOutcome.NotFound => WorkflowAuditCodes.ApprovalNotFound,
        ApprovalOutcome.ActionMismatch => WorkflowAuditCodes.ApprovalActionMismatch,
        ApprovalOutcome.VendorMismatch => WorkflowAuditCodes.ApprovalVendorMismatch,
        ApprovalOutcome.PolicySuperseded => WorkflowAuditCodes.ApprovalPolicySuperseded,
        ApprovalOutcome.EvidenceSuperseded => WorkflowAuditCodes.ApprovalEvidenceSuperseded,
        ApprovalOutcome.NotYetValid => WorkflowAuditCodes.ApprovalNotYetValid,
        ApprovalOutcome.Expired => WorkflowAuditCodes.ApprovalExpired,
        ApprovalOutcome.SelfApproval => WorkflowAuditCodes.ApprovalSelfApproval,
        ApprovalOutcome.WrongRole => WorkflowAuditCodes.ApprovalWrongRole,
        _ => throw new InvalidOperationException("A valid approval cannot produce a rejection code.")
    };
}
