using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Core.Ports;

/// <summary>
/// Evaluates a validated structured action and normalized facts without receiving evidence-document prose.
/// </summary>
public interface IRiskEvaluator
{
    /// <summary>
    /// Produces a deterministic assessment from the requested action and scoped typed facts.
    /// </summary>
    /// <param name="input">The structured, prose-free risk input.</param>
    /// <returns>A structured deterministic risk assessment.</returns>
    RiskEvaluation EvaluateRisk(RiskEvaluationInput input);
}
