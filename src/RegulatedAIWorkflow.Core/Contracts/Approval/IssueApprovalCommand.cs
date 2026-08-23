using RegulatedAIWorkflow.Core.Contracts.Workflow;

namespace RegulatedAIWorkflow.Core.Contracts.Approval;

/// <summary>
/// Requests a stored approval for the current evidence and policy state.
/// </summary>
public sealed record IssueApprovalCommand(
    string? VendorId,
    WorkflowAction RequestedAction,
    int? ValidForHours = null);
