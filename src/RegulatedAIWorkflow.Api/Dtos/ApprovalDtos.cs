using RegulatedAIWorkflow.Core.Contracts.Approval;
using RegulatedAIWorkflow.Core.Contracts.Workflow;

namespace RegulatedAIWorkflow.Api.Dtos;

/// <summary>Contains the untrusted body of an approval request.</summary>
public sealed record ApprovalRequest(
    string? VendorId,
    WorkflowAction RequestedAction,
    int? ValidForHours = null);

/// <summary>Contains a successfully recorded approval.</summary>
public sealed record ApprovalResponse(
    string ApprovalId,
    string ApproverUserId,
    string VendorId,
    string RequestedAction,
    string EvidenceSetHash,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string RiskPolicyVersion)
{
    /// <summary>Maps a successful Core approval result to its wire representation.</summary>
    public static ApprovalResponse FromCore(ApprovalIssueResult result)
    {
        if (result.Outcome is not ApprovalIssueOutcome.Issued)
        {
            throw new ArgumentException("Only an issued approval has a success response.", nameof(result));
        }

        return new ApprovalResponse(
            result.ApprovalId ?? throw MissingBinding(nameof(result.ApprovalId)),
            result.ApproverUserId ?? throw MissingBinding(nameof(result.ApproverUserId)),
            result.VendorId ?? throw MissingBinding(nameof(result.VendorId)),
            result.RequestedAction switch
            {
                WorkflowAction.MarkVendorApproved => "markVendorApproved",
                _ => throw MissingBinding(nameof(result.RequestedAction))
            },
            result.EvidenceSetHash ?? throw MissingBinding(nameof(result.EvidenceSetHash)),
            result.IssuedAtUtc ?? throw MissingBinding(nameof(result.IssuedAtUtc)),
            result.ExpiresAtUtc ?? throw MissingBinding(nameof(result.ExpiresAtUtc)),
            result.RiskPolicyVersion ?? throw MissingBinding(nameof(result.RiskPolicyVersion)));
    }

    private static InvalidOperationException MissingBinding(string field) =>
        new($"An issued approval did not contain {field}.");
}
