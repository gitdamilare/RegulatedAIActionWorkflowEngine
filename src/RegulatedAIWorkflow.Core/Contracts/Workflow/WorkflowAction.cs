namespace RegulatedAIWorkflow.Core.Contracts.Workflow;

/// <summary>
/// Identifies an action that a workflow may evaluate.
/// </summary>
public enum WorkflowAction
{
    /// <summary>No recognized action was requested.</summary>
    Unknown,

    /// <summary>Mark a vendor as approved to process payment data.</summary>
    MarkVendorApproved
}
