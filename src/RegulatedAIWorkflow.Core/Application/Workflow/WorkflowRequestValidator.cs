using RegulatedAIWorkflow.Core.Contracts.Workflow;

namespace RegulatedAIWorkflow.Core.Application.Workflow;

/// <summary>
/// Applies bounded, transport-independent validation to workflow identity and request data.
/// </summary>
internal static class WorkflowRequestValidator
{
    internal const int MaximumIdentifierLength = 128;
    internal const int MaximumQuestionLength = 2_000;

    internal static ValidatedWorkflowRequest? Validate(
        WorkflowPrincipal? principal,
        WorkflowCommand? command)
    {
        if (principal is null ||
            command is null ||
            !IsValidIdentifier(principal.TenantId) ||
            !IsValidIdentifier(principal.UserId) ||
            !Enum.IsDefined(principal.Role) ||
            principal.Role is UserRole.Unknown ||
            !IsValidIdentifier(command.VendorId) ||
            !Enum.IsDefined(command.RequestedAction) ||
            command.RequestedAction is WorkflowAction.Unknown ||
            !IsValidOptionalIdentifier(command.ApprovalId) ||
            !IsValidQuestion(command.Question))
        {
            return null;
        }

        return new ValidatedWorkflowRequest(
            principal.TenantId,
            principal.UserId,
            principal.Role,
            command.VendorId!,
            command.RequestedAction,
            command.ApprovalId);
    }

    internal static string? SafeIdentifierOrNull(string? value) =>
        IsValidIdentifier(value) ? value : null;

    internal static bool IsValidIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= MaximumIdentifierLength &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
        !value.Any(char.IsControl);

    private static bool IsValidOptionalIdentifier(string? value) =>
        value is null || IsValidIdentifier(value);

    private static bool IsValidQuestion(string? value) =>
        value is null ||
        (value.Length <= MaximumQuestionLength && !value.Any(char.IsControl));
}

/// <summary>
/// Carries only validated identity, scope, and action data into the workflow.
/// </summary>
internal sealed record ValidatedWorkflowRequest(
    string TenantId,
    string UserId,
    UserRole Role,
    string VendorId,
    WorkflowAction RequestedAction,
    string? ApprovalId);
