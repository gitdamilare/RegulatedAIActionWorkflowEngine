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
        var identity = IdentityHeaderBinder.Bind(request.Headers);
        if (identity.Failure is not IdentityBindingFailure.None)
        {
            return identity.Failure.ToProblem();
        }

        var command = new WorkflowCommand(
            body.VendorId,
            body.Question,
            body.RequestedAction,
            body.ApprovalId);
        var result = await orchestrator.RunAsync(
            identity.Principal,
            command,
            cancellationToken);

        return Results.Json(
            WorkflowResponse.FromCore(result),
            statusCode: StatusCode(result.ActionStatus));
    }

    private static int StatusCode(ActionStatus status) => status switch
    {
        ActionStatus.BlockedInvalidRequest => StatusCodes.Status400BadRequest,
        ActionStatus.BlockedUnauthorized => StatusCodes.Status403Forbidden,
        ActionStatus.DeniedUnknownSubject => StatusCodes.Status200OK,
        ActionStatus.BlockedPendingApproval => StatusCodes.Status200OK,
        ActionStatus.BlockedEvidenceUnavailable => StatusCodes.Status200OK,
        ActionStatus.BlockedExecutionUnavailable => StatusCodes.Status503ServiceUnavailable,
        ActionStatus.Executed => StatusCodes.Status200OK,
        _ => throw new InvalidOperationException("The workflow status has no HTTP mapping.")
    };

}
