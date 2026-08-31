using RegulatedAIWorkflow.Core.Contracts.Workflow;

namespace RegulatedAIWorkflow.Core.Contracts.Approval;

/// <summary>
/// The stored artifact that authorizes one regulated effect. The server creates it; a caller can only
/// present its id. Binding it to tenant, vendor, action, a named approver, the evidence set that was on
/// the table, and a validity window is what makes it an approval of a decision rather than of a vendor.
/// </summary>
public sealed record ApprovalRecord(
    string ApprovalId,
    string TenantId,
    string VendorId,
    WorkflowAction Action,
    string ApproverUserId,
    UserRole ApproverRole,
    string EvidenceSetHash,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc);
