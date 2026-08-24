using RegulatedAIWorkflow.Core.Application.Workflow;
using RegulatedAIWorkflow.Core.Contracts.Approval;
using RegulatedAIWorkflow.Core.Contracts.Audit;
using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Approval;
using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Risk;
using RegulatedAIWorkflow.Core.Ports;

namespace RegulatedAIWorkflow.Core.Application.Approval;

/// <summary>
/// Issues a stored approval bound to the current tenant evidence and risk policy.
/// </summary>
public sealed class ApprovalIssuer(
    IEvidenceRepository evidenceRepository,
    IRiskEvaluator riskEvaluator,
    IApprovalRepository approvalRepository,
    IAuditSink auditSink,
    TimeProvider timeProvider)
{
    /// <summary>Validates, binds, stores, and audits an approval.</summary>
    public async Task<ApprovalIssueResult> IssueAsync(
        WorkflowPrincipal? approver,
        IssueApprovalCommand? command,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.CreateVersion7();
        var validated = ApprovalRequestValidator.Validate(approver, command);

        if (validated is null)
        {
            await WriteAuditAsync(
                correlationId,
                approver,
                command,
                AuditOutcome.ApprovalRejected,
                [WorkflowAuditCodes.InvalidRequest]);
            return EmptyResult(ApprovalIssueOutcome.InvalidRequest, command);
        }

        if (!ActionAuthorizationPolicy.MayApprove(validated.Role, validated.RequestedAction))
        {
            await WriteAuditAsync(
                correlationId,
                approver,
                command,
                AuditOutcome.ApprovalRejected,
                [WorkflowAuditCodes.ApproverRoleInsufficient]);
            return EmptyResult(ApprovalIssueOutcome.ApproverRoleInsufficient, command);
        }

        var retrieved = await evidenceRepository.SearchEvidenceAsync(
            new EvidenceQuery(validated.TenantId, validated.VendorId),
            cancellationToken);
        var scoped = EvidenceSecurity.EnforceScope(
            retrieved,
            validated.TenantId,
            validated.VendorId);

        if (scoped.HadOutOfScopeContent)
        {
            await WriteAuditAsync(
                correlationId,
                approver,
                command,
                AuditOutcome.ApprovalRejected,
                [WorkflowAuditCodes.EvidenceGateFailed]);
            return EmptyResult(ApprovalIssueOutcome.EvidenceUnavailable, command);
        }

        if (scoped.Evidence.Documents.Count == 0)
        {
            await WriteAuditAsync(
                correlationId,
                approver,
                command,
                AuditOutcome.ApprovalRejected,
                [WorkflowAuditCodes.VendorNotFound]);
            return EmptyResult(ApprovalIssueOutcome.VendorNotFound, command);
        }

        var evaluation = riskEvaluator.EvaluateRisk(new RiskEvaluationInput(
            validated.RequestedAction,
            scoped.Evidence.Facts,
            HasScopedEvidence: true));
        if (!Enum.IsDefined(evaluation.RiskLevel) ||
            evaluation.RiskLevel is RiskLevel.Unknown ||
            !WorkflowRequestValidator.IsValidIdentifier(evaluation.PolicyVersion))
        {
            throw new InvalidOperationException("The risk evaluator returned an invalid approval binding.");
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var evidenceSetHash = CanonicalEvidenceHasher.Compute(
            validated.TenantId,
            validated.VendorId,
            scoped.Evidence,
            evaluation.PolicyVersion);
        var approval = new ApprovalRecord(
            $"apr-{Guid.CreateVersion7():N}",
            validated.TenantId,
            validated.VendorId,
            validated.RequestedAction,
            validated.UserId,
            validated.Role,
            evidenceSetHash,
            evaluation.PolicyVersion,
            now,
            now.AddHours(validated.ValidForHours));

        await approvalRepository.SaveAsync(approval, cancellationToken);
        await WriteAuditAsync(
            correlationId,
            approver,
            command,
            AuditOutcome.ApprovalRecorded,
            [],
            approval);

        return new ApprovalIssueResult(
            ApprovalIssueOutcome.Issued,
            approval.ApprovalId,
            approval.ApproverUserId,
            approval.VendorId,
            approval.Action,
            approval.EvidenceSetHash,
            approval.IssuedAtUtc,
            approval.ExpiresAtUtc,
            approval.RiskPolicyVersion);
    }

    private static ApprovalIssueResult EmptyResult(
        ApprovalIssueOutcome outcome,
        IssueApprovalCommand? command) =>
        new(
            outcome,
            ApprovalId: null,
            ApproverUserId: null,
            command?.VendorId,
            command is not null && Enum.IsDefined(command.RequestedAction)
                ? command.RequestedAction
                : WorkflowAction.Unknown,
            EvidenceSetHash: null,
            IssuedAtUtc: null,
            ExpiresAtUtc: null,
            RiskPolicyVersion: null);

    private Task WriteAuditAsync(
        Guid correlationId,
        WorkflowPrincipal? approver,
        IssueApprovalCommand? command,
        AuditOutcome outcome,
        IReadOnlyList<string> reasonCodes,
        ApprovalRecord? approval = null)
    {
        var auditEvent = new AuditEvent(
            Guid.CreateVersion7(),
            correlationId,
            timeProvider.GetUtcNow().ToUniversalTime(),
            WorkflowRequestValidator.SafeIdentifierOrNull(approver?.TenantId),
            WorkflowRequestValidator.SafeIdentifierOrNull(approver?.UserId),
            approver is not null && Enum.IsDefined(approver.Role)
                ? approver.Role
                : UserRole.Unknown,
            WorkflowRequestValidator.SafeIdentifierOrNull(command?.VendorId),
            AuditEventType.ApprovalDecision,
            command is not null && Enum.IsDefined(command.RequestedAction)
                ? command.RequestedAction
                : WorkflowAction.Unknown,
            RiskLevel: null,
            outcome,
            ReferencedDocumentIds: [],
            reasonCodes,
            MissingEvidenceCodes: [],
            approval?.RiskPolicyVersion,
            approval?.ApprovalId,
            approval?.ApproverUserId);

        return auditSink.WriteAuditEventAsync(auditEvent, CancellationToken.None);
    }
}
