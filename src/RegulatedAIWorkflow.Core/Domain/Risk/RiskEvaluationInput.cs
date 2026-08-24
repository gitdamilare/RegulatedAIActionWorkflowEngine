using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Evidence;

namespace RegulatedAIWorkflow.Core.Domain.Risk;

/// <summary>
/// Supplies deterministic risk policy with a structured action, normalized facts, and scope state only.
/// </summary>
/// <param name="RequestedAction">The validated server-recognized action being assessed.</param>
/// <param name="Facts">The scoped, source-attributed facts to evaluate.</param>
/// <param name="HasScopedEvidence">Whether trustworthy evidence remains after scope checks.</param>
public sealed record RiskEvaluationInput(
    WorkflowAction RequestedAction,
    IReadOnlyList<EvidenceFact> Facts,
    bool HasScopedEvidence);
