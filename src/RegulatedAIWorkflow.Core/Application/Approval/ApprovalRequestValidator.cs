using RegulatedAIWorkflow.Core.Application.Workflow;
using RegulatedAIWorkflow.Core.Contracts.Approval;
using RegulatedAIWorkflow.Core.Contracts.Workflow;

namespace RegulatedAIWorkflow.Core.Application.Approval;

internal static class ApprovalRequestValidator
{
    internal static ValidatedApprovalRequest? Validate(
        WorkflowPrincipal? principal,
        IssueApprovalCommand? command)
    {
        var validForHours = command?.ValidForHours ?? 24;
        if (principal is null ||
            command is null ||
            !WorkflowRequestValidator.IsValidIdentifier(principal.TenantId) ||
            !WorkflowRequestValidator.IsValidIdentifier(principal.UserId) ||
            !Enum.IsDefined(principal.Role) ||
            principal.Role is UserRole.Unknown ||
            !WorkflowRequestValidator.IsValidIdentifier(command.VendorId) ||
            !Enum.IsDefined(command.RequestedAction) ||
            command.RequestedAction is WorkflowAction.Unknown ||
            validForHours is < 1 or > 168)
        {
            return null;
        }

        return new ValidatedApprovalRequest(
            principal.TenantId,
            principal.UserId,
            principal.Role,
            command.VendorId!,
            command.RequestedAction,
            validForHours);
    }
}

internal sealed record ValidatedApprovalRequest(
    string TenantId,
    string UserId,
    UserRole Role,
    string VendorId,
    WorkflowAction RequestedAction,
    int ValidForHours);
