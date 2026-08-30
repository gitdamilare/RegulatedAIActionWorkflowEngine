using RegulatedAIWorkflow.Core.Application.Approval;
using RegulatedAIWorkflow.Core.Contracts.Audit;
using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Execution;
using RegulatedAIWorkflow.Core.Domain.Risk;
using RegulatedAIWorkflow.Core.Ports;

namespace RegulatedAIWorkflow.Core.Application.Workflow;

/// <summary>
/// Runs the workflow through its gates in a fixed order. The order is the design: authorization precedes
/// retrieval, policy reads only typed facts, approval precedes the effect, and the attempt is audited
/// before the effect can begin. Every run writes exactly two audit events.
/// </summary>
public sealed class WorkflowOrchestrator(
    IEvidenceRepository evidenceRepository,
    IRiskEvaluator riskEvaluator,
    ApprovalGate approvalGate,
    IAuditSink auditSink,
    IActionExecutor actionExecutor,
    TimeProvider timeProvider)
{
    /// <summary>Validates, authorizes, assesses, verifies, audits, and conditionally executes a request.</summary>
    public async Task<WorkflowRunResult> RunAsync(
        WorkflowPrincipal? principal,
        WorkflowCommand? command,
        CancellationToken cancellationToken = default)
    {
        var workflowId = Guid.CreateVersion7();
        var audit = new WorkflowAuditRecorder(workflowId, auditSink, timeProvider)
        {
            TenantId = WorkflowRequestValidator.SafeIdentifierOrNull(principal?.TenantId),
            ActorUserId = WorkflowRequestValidator.SafeIdentifierOrNull(principal?.UserId),
            ActorRole = principal is not null && Enum.IsDefined(principal.Role) ? principal.Role : UserRole.Unknown,
            VendorId = WorkflowRequestValidator.SafeIdentifierOrNull(command?.VendorId),
            RequestedAction = command is not null && Enum.IsDefined(command.RequestedAction)
                ? command.RequestedAction
                : WorkflowAction.Unknown
        };
        var executorCallOutstanding = false;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 1. Validate, reducing the request to safe identity, scope, and action data.
            var request = WorkflowRequestValidator.Validate(principal, command);
            if (request is null)
            {
                audit.ReasonCodes = [WorkflowAuditCodes.InvalidRequest];
                return await audit.CompleteAsync(
                    WorkflowRunResult.Refused(workflowId, ActionStatus.BlockedInvalidRequest),
                    AuditOutcome.InvalidRequest);
            }

            audit.TenantId = request.TenantId;
            audit.ActorUserId = request.UserId;
            audit.ActorRole = request.Role;
            audit.VendorId = request.VendorId;
            audit.RequestedAction = request.RequestedAction;
            audit.ApprovalId = request.ApprovalId;

            // 2. Authorize before any evidence can be retrieved. Deny by default.
            if (!WorkflowActionPolicies.MayAttempt(request.Role, request.RequestedAction))
            {
                audit.ReasonCodes = [WorkflowAuditCodes.RoleNotAuthorized];
                return await audit.CompleteAsync(
                    WorkflowRunResult.Refused(workflowId, ActionStatus.BlockedUnauthorized),
                    AuditOutcome.BlockedUnauthorized);
            }

            // 3. Retrieve with scope as a query parameter, never as a filter applied afterwards.
            var query = new EvidenceQuery(request.TenantId, request.VendorId);
            var documents = await evidenceRepository.SearchEvidenceAsync(query, cancellationToken);

            // 4. Defence in depth, against the same definition of scope the adapter was given. A leaky
            //    adapter is a bug, not a branch: fail loudly, never filter quietly.
            if (documents.Any(document => !query.Covers(document)))
            {
                throw new InvalidOperationException("The evidence repository returned out-of-scope content.");
            }

            if (documents.Count == 0)
            {
                audit.ReasonCodes = [WorkflowAuditCodes.UnknownSubject];
                return await audit.CompleteAsync(
                    WorkflowRunResult.UnknownSubject(workflowId, WorkflowAuditCodes.UnknownSubject),
                    AuditOutcome.DeniedUnknownSubject);
            }

            // 5. Evaluate typed facts only. Snippet prose has no representation in the input type.
            var facts = documents
                .SelectMany(document => document.FactTypes
                    .Select(factType => new EvidenceFact(document.DocumentId, factType)))
                .ToArray();
            var evaluation = riskEvaluator.EvaluateRisk(new RiskEvaluationInput(request.RequestedAction, facts));

            if (!Enum.IsDefined(evaluation.RiskLevel) || evaluation.RiskLevel is RiskLevel.Unknown)
            {
                throw new InvalidOperationException("The risk evaluator returned an invalid risk level.");
            }

            // 6. Attach display snippets, and only for documents that were actually retained. ForDisplay
            //    is the one call that turns untrusted prose into text a caller may see.
            var retained = documents.ToDictionary(d => d.DocumentId, StringComparer.Ordinal);
            var citations = evaluation.CitedDocumentIds
                .Where(retained.ContainsKey)
                .Select(documentId => new Citation(documentId, retained[documentId].UntrustedSnippet.ForDisplay()))
                .ToArray();

            audit.RiskLevel = evaluation.RiskLevel;
            audit.ReferencedDocumentIds = citations.Select(citation => citation.DocumentId).ToArray();
            audit.ReasonCodes = evaluation.Reasons.Select(reason => reason.Code).ToArray();

            // 7. A high-risk action needs a stored approval issued for exactly this request.
            if (evaluation.RequiresApproval)
            {
                var decision = await approvalGate.VerifyAsync(
                    new WorkflowPrincipal(request.TenantId, request.UserId, request.Role),
                    request.VendorId,
                    request.RequestedAction,
                    request.ApprovalId,
                    cancellationToken);

                audit.ApproverUserId = decision.Approval?.ApproverUserId;
                if (!decision.IsApproved)
                {
                    audit.ReasonCodes = [.. audit.ReasonCodes, decision.ReasonCode];
                    return await audit.CompleteAsync(
                        WorkflowRunResult.Assessed(workflowId, evaluation, citations, ActionStatus.BlockedPendingApproval),
                        AuditOutcome.BlockedPendingApproval);
                }
            }

            // 8. Persist the attempt before the effect. If this write fails, nothing runs.
            await audit.WriteAsync(AuditEventType.ActionAttempt, AuditOutcome.AuthorizedForExecution);

            // 9. Execute. The last step, and the only one that changes the world.
            executorCallOutstanding = true;
            await actionExecutor.ExecuteAsync(
                new ActionExecutionRequest(
                    workflowId,
                    request.TenantId,
                    request.VendorId,
                    request.UserId,
                    request.RequestedAction),
                cancellationToken);
            executorCallOutstanding = false;

            // 10. Record the terminal outcome and return.
            await audit.WriteAsync(AuditEventType.WorkflowCompleted, AuditOutcome.Executed);
            return WorkflowRunResult.Assessed(workflowId, evaluation, citations, ActionStatus.Executed)
                with
            { AuditEventIds = audit.EventIds };
        }
        catch
        {
            // A failure after dispatch does not prove the effect did not happen. Never record it as Failed.
            if (executorCallOutstanding)
            {
                audit.ReasonCodes = [.. audit.ReasonCodes, WorkflowAuditCodes.ExecutionOutcomeUnknown];
            }

            await audit.WriteAsync(
                AuditEventType.WorkflowCompleted,
                executorCallOutstanding ? AuditOutcome.ExecutionOutcomeUnknown : AuditOutcome.Failed);
            throw;
        }
    }
}
