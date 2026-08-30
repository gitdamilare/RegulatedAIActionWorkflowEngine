using RegulatedAIWorkflow.Api.Dtos;
using RegulatedAIWorkflow.Api.Identity;
using RegulatedAIWorkflow.Core.Application.Approval;
using RegulatedAIWorkflow.Core.Contracts.Approval;

namespace RegulatedAIWorkflow.Api.Endpoints;

internal static class ApprovalEndpoint
{
    internal static async Task<IResult> RecordAsync(
        HttpRequest request,
        ApprovalRequest body,
        ApprovalIssuer approvalIssuer,
        CancellationToken cancellationToken)
    {
        if (!IdentityHeaderBinder.TryBind(request.Headers, out var principal, out var problem))
        {
            return problem!;
        }

        var result = await approvalIssuer.IssueAsync(
            principal,
            body.VendorId,
            body.RequestedAction,
            cancellationToken);

        return result.Outcome switch
        {
            ApprovalIssueOutcome.Issued => Results.Json(
                ApprovalResponse.FromCore(result.Approval!),
                statusCode: StatusCodes.Status201Created),
            ApprovalIssueOutcome.InvalidRequest => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The approval request is invalid."),
            ApprovalIssueOutcome.ApproverRoleInsufficient => Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "The caller cannot approve this action."),
            _ => throw new InvalidOperationException("Unhandled approval issue outcome.")
        };
    }
}
