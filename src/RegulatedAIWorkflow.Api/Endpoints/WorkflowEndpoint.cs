using RegulatedAIWorkflow.Api.Dtos;
using RegulatedAIWorkflow.Api.Identity;
using RegulatedAIWorkflow.Core.Application.Workflow;
using RegulatedAIWorkflow.Core.Contracts.Workflow;

namespace RegulatedAIWorkflow.Api.Endpoints;

internal static class WorkflowEndpoint
{
    internal static async Task<IResult> RunAsync(
        HttpRequest request,
        WorkflowRequest body,
        WorkflowOrchestrator orchestrator,
        CancellationToken cancellationToken)
    {
        if (!IdentityHeaderBinder.TryBind(request.Headers, out var principal, out var problem))
        {
            return problem!;
        }

        var result = await orchestrator.RunAsync(
            principal,
            new WorkflowCommand(body.VendorId, body.Question, body.RequestedAction, body.ApprovalId),
            cancellationToken);

        return Results.Json(WorkflowResponse.FromCore(result), statusCode: StatusCode(result.ActionStatus));
    }

    // A refusal is a successful evaluation: the caller needs the reasons, gaps, citations, and audit ids,
    // so it returns 200 with a full body. An unknown subject returns 200 for the same reason a 403 would
    // be wrong: a distinguishable response would confirm the vendor exists in another tenant.
    //
    // Unverifiable citations are the exception. The assessment and the evidence disagreed, so nothing was
    // established and there is no refusal for the caller to act on: 200 would claim an evaluation that did
    // not happen, and a 4xx would blame a caller who did nothing wrong. It is a server fault, reported as
    // one, with the workflow and audit ids retained so the run can still be traced.
    private static int StatusCode(ActionStatus status) => status switch
    {
        ActionStatus.BlockedInvalidRequest => StatusCodes.Status400BadRequest,
        ActionStatus.BlockedUnauthorized => StatusCodes.Status403Forbidden,
        ActionStatus.DeniedUnknownSubject => StatusCodes.Status200OK,
        ActionStatus.BlockedEvidenceUnavailable => StatusCodes.Status500InternalServerError,
        ActionStatus.BlockedPendingApproval => StatusCodes.Status200OK,
        ActionStatus.Executed => StatusCodes.Status200OK,
        _ => throw new InvalidOperationException("The workflow status has no HTTP mapping.")
    };
}
