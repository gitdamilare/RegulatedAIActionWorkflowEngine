namespace RegulatedAIWorkflow.Api.Identity;

internal static class IdentityBindingFailureExtensions
{
    internal static IResult ToProblem(this IdentityBindingFailure failure) => failure switch
    {
        IdentityBindingFailure.Missing => Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Caller identity is required."),
        IdentityBindingFailure.Invalid => Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Caller identity is invalid."),
        _ => throw new ArgumentOutOfRangeException(nameof(failure))
    };
}
