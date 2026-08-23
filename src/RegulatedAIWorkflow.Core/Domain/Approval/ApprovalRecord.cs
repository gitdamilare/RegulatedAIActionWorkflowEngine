using RegulatedAIWorkflow.Core.Contracts.Workflow;

namespace RegulatedAIWorkflow.Core.Domain.Approval;

/// <summary>
/// Binds an approver's authority to an exact tenant, action, evidence set, policy, and time window.
/// </summary>
public sealed record ApprovalRecord(
    string ApprovalId,
    string TenantId,
    string VendorId,
    WorkflowAction Action,
    string ApproverUserId,
    UserRole ApproverRole,
    string EvidenceSetHash,
    string RiskPolicyVersion,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc);
