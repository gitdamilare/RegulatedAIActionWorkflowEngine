using RegulatedAIWorkflow.Core.Contracts.Workflow;

namespace RegulatedAIWorkflow.Core.Contracts.Approval;

/// <summary>
/// Returns safe structured details about an approval issuance attempt.
/// </summary>
public sealed record ApprovalIssueResult(
    ApprovalIssueOutcome Outcome,
    string? ApprovalId,
    string? ApproverUserId,
    string? VendorId,
    WorkflowAction RequestedAction,
    string? EvidenceSetHash,
    DateTimeOffset? IssuedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    string? RiskPolicyVersion);
