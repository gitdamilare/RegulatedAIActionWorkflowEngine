using RegulatedAIWorkflow.Core.Contracts.Workflow;

namespace RegulatedAIWorkflow.Core.Contracts.Approval;

/// <summary>
/// The stored artifact that authorizes one regulated effect. The server creates it; a caller can only
/// present its id. Binding it to tenant, vendor, action, and a named approver is what makes it an
/// approval rather than a caller-supplied claim.
/// </summary>
public sealed record ApprovalRecord(
    string ApprovalId,
    string TenantId,
    string VendorId,
    WorkflowAction Action,
    string ApproverUserId,
    UserRole ApproverRole,
    DateTimeOffset IssuedAtUtc);
