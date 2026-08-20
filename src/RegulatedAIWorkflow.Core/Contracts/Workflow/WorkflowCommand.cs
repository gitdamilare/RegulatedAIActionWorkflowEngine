namespace RegulatedAIWorkflow.Core.Contracts.Workflow;

/// <summary>
/// Captures the untrusted request data needed to evaluate a workflow.
/// </summary>
/// <param name="VendorId">The requested vendor identifier.</param>
/// <param name="Question">The caller's optional question, which has no policy authority.</param>
/// <param name="RequestedAction">The action to evaluate.</param>
public sealed record WorkflowCommand(
    string? VendorId,
    string? Question,
    WorkflowAction RequestedAction);
