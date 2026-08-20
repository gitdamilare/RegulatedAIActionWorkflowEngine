using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Core.Ports;

/// <summary>
/// Evaluates normalized facts without receiving evidence-document prose.
/// </summary>
public interface IRiskEvaluator
{
    /// <summary>
    /// Produces a deterministic assessment from scoped typed facts.
    /// </summary>
    /// <param name="input">The fact-only risk input.</param>
    /// <returns>A structured deterministic risk assessment.</returns>
    RiskEvaluation EvaluateRisk(RiskEvaluationInput input);
}
