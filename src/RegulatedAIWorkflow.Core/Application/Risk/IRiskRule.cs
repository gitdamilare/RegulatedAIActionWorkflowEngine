namespace RegulatedAIWorkflow.Core.Application.Risk;

/// <summary>
/// Evaluates one named policy condition against typed facts. A rule that does not apply returns null,
/// and every rule that does apply contributes to the same assessment: there is no early exit, because a
/// caller needs every gap rather than the first one.
/// </summary>
internal interface IRiskRule
{
    /// <summary>Returns an outcome when the policy condition applies, otherwise null.</summary>
    RiskRuleOutcome? Evaluate(RiskRuleContext context);
}
