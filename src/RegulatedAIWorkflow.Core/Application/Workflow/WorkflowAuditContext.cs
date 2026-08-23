using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Core.Application.Workflow;

internal sealed class WorkflowAuditContext(Guid workflowId)
{
    public Guid WorkflowId { get; } = workflowId;

    public List<Guid> EventIds { get; } = [];

    public string? TenantId { get; set; }

    public string? ActorUserId { get; set; }

    public UserRole ActorRole { get; set; }

    public string? VendorId { get; set; }

    public WorkflowAction RequestedAction { get; set; }

    public RiskLevel? RiskLevel { get; set; }

    public string? PolicyVersion { get; set; }

    public IReadOnlyList<string> ReferencedDocumentIds { get; set; } = [];

    public IReadOnlyList<string> ReasonCodes { get; set; } = [];

    public IReadOnlyList<string> MissingEvidenceCodes { get; set; } = [];

    public string? ApprovalId { get; set; }

    public string? ApproverUserId { get; set; }
}
