namespace RegulatedAIWorkflow.Core.Contracts.Workflow;

/// <summary>
/// Identifies an action that a workflow may evaluate. Adding one is a new member here and a new entry in
/// <see cref="Application.WorkflowActionPolicies"/>; nothing else in the pipeline needs to change.
/// </summary>
public enum WorkflowAction
{
    /// <summary>No recognized action was requested.</summary>
    Unknown,

    /// <summary>Mark a vendor as approved to process payment data.</summary>
    MarkVendorApproved,

    /// <summary>Ask a vendor for the evidence an assessment found missing. Low consequence, and reversible.</summary>
    RequestVendorEvidence
}
