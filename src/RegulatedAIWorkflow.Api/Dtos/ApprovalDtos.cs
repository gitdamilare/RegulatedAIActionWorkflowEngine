using System.Text.Json;
using RegulatedAIWorkflow.Core.Contracts.Approval;
using RegulatedAIWorkflow.Core.Contracts.Workflow;

namespace RegulatedAIWorkflow.Api.Dtos;

/// <summary>The untrusted body of an approval request. The approver's identity comes from headers, not from here.</summary>
public sealed record ApprovalRequest(string? VendorId, WorkflowAction RequestedAction);

/// <summary>The wire representation of a recorded approval.</summary>
public sealed record ApprovalResponse(
    string ApprovalId,
    string TenantId,
    string VendorId,
    string RequestedAction,
    string ApproverUserId,
    string EvidenceSetHash,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc)
{
    /// <summary>Maps a stored approval to its wire representation.</summary>
    public static ApprovalResponse FromCore(ApprovalRecord approval)
    {
        ArgumentNullException.ThrowIfNull(approval);

        return new ApprovalResponse(
            approval.ApprovalId,
            approval.TenantId,
            approval.VendorId,
            JsonNamingPolicy.CamelCase.ConvertName(approval.Action.ToString()),
            approval.ApproverUserId,
            approval.EvidenceSetHash,
            approval.IssuedAtUtc,
            approval.ExpiresAtUtc);
    }
}
