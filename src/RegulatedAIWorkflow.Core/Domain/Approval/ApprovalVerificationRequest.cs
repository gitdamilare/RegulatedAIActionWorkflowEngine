using RegulatedAIWorkflow.Core.Contracts.Workflow;

namespace RegulatedAIWorkflow.Core.Domain.Approval;

/// <summary>
/// Supplies the current trusted request and assessment bindings to the approval gate.
/// </summary>
public sealed record ApprovalVerificationRequest(
    WorkflowPrincipal Requester,
    string VendorId,
    WorkflowAction RequestedAction,
    string EvidenceSetHash,
    string RiskPolicyVersion,
    string? ApprovalId);
