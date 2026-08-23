using RegulatedAIWorkflow.Core.Contracts.Workflow;

namespace RegulatedAIWorkflow.Core.Domain.Execution;

/// <summary>
/// Carries only validated structured data to an action executor.
/// </summary>
public sealed record ActionExecutionRequest(
    Guid WorkflowId,
    string TenantId,
    string VendorId,
    string RequestingUserId,
    WorkflowAction Action);
