using RegulatedAIWorkflow.Core.Contracts.Workflow;

namespace RegulatedAIWorkflow.Core.Domain.Execution;

/// <summary>The validated, authorized, approved instruction handed to the executor.</summary>
public sealed record ActionExecutionRequest(
    Guid WorkflowId,
    string TenantId,
    string VendorId,
    string ActorUserId,
    WorkflowAction RequestedAction);
