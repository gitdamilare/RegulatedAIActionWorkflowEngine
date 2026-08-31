using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Evidence;

namespace RegulatedAIWorkflow.Core.Domain.Risk;

/// <summary>
/// The complete input to deterministic risk policy: a validated action and typed, source-attributed facts.
/// It carries no question, snippet, or other free text, so evidence prose has no path into a policy decision.
/// </summary>
public sealed record RiskEvaluationInput(
    WorkflowAction RequestedAction,
    IReadOnlyList<EvidenceFact> Facts);
