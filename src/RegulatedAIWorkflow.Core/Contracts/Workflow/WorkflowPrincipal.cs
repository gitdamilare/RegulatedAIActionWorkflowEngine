namespace RegulatedAIWorkflow.Core.Contracts.Workflow;

/// <summary>
/// Identifies the tenant-scoped caller of a workflow.
/// </summary>
/// <param name="TenantId">The tenant under which the caller acts.</param>
/// <param name="UserId">The caller's stable identifier.</param>
/// <param name="Role">The caller's asserted role.</param>
public sealed record WorkflowPrincipal(string TenantId, string UserId, UserRole Role);
