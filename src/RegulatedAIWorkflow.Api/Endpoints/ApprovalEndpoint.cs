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
        var identity = IdentityHeaderBinder.Bind(request.Headers);
        if (identity.Failure is not IdentityBindingFailure.None)
        {
            return identity.Failure.ToProblem();
        }

        var command = new IssueApprovalCommand(
            body.VendorId,
            body.RequestedAction,
            body.ValidForHours);
        var result = await approvalIssuer.IssueAsync(
            identity.Principal,
            command,
            cancellationToken);

        return result.Outcome switch
        {
            ApprovalIssueOutcome.Issued => Results.Json(
                ApprovalResponse.FromCore(result),
                statusCode: StatusCodes.Status201Created),
            ApprovalIssueOutcome.InvalidRequest => Problem(
                StatusCodes.Status400BadRequest,
                "The approval request is invalid."),
            ApprovalIssueOutcome.ApproverRoleInsufficient => Problem(
                StatusCodes.Status403Forbidden,
                "The caller cannot approve this action."),
            ApprovalIssueOutcome.VendorNotFound => Problem(
                StatusCodes.Status404NotFound,
                "The requested vendor was not found."),
            ApprovalIssueOutcome.EvidenceUnavailable => Problem(
                StatusCodes.Status503ServiceUnavailable,
                "Trustworthy evidence is unavailable."),
            _ => throw new InvalidOperationException("The approval outcome has no HTTP mapping.")
        };
    }

    private static IResult Problem(int statusCode, string title) =>
        Results.Problem(statusCode: statusCode, title: title);
}
