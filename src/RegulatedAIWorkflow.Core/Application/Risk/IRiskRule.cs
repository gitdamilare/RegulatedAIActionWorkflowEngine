namespace RegulatedAIWorkflow.Core.Application.Risk;

/// <summary>
/// Evaluates one deterministic policy condition against typed facts.
/// </summary>
internal interface IRiskRule
{
    /// <summary>Returns an outcome when the policy condition applies.</summary>
    RiskRuleOutcome? Evaluate(RiskRuleContext context);
}
