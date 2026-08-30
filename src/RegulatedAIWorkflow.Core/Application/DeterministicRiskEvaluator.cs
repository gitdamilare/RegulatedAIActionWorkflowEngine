using RegulatedAIWorkflow.Core.Application.Risk;
using RegulatedAIWorkflow.Core.Application.Risk.Rules;
using RegulatedAIWorkflow.Core.Domain.Risk;
using RegulatedAIWorkflow.Core.Ports;

namespace RegulatedAIWorkflow.Core.Application;

/// <summary>
/// Server-owned risk policy. Reads only typed facts, so no evidence prose can reach a rule condition.
/// Effective risk is the maximum of the action baseline and every rule that fires; a rule can raise the
/// level but never lower it, and no rule short-circuits the rest.
/// </summary>
public sealed class DeterministicRiskEvaluator : IRiskEvaluator
{
    /// <summary>
    /// The policy, in evaluation order. That order is also citation order, so the facts that make a
    /// decision regulated are named before the gaps found within it. Adding a condition is one new rule
    /// class and one line here.
    /// </summary>
    private static readonly IRiskRule[] Rules =
    [
        new PaymentDataScopeRule(),
        new SensitiveDataScopeRule(),
        new PaymentSecurityRequirementRule(),
        new MissingSoc2Rule(),
        new MissingRetentionScheduleRule(),
        new MissingBreachNotificationRule()
    ];

    /// <inheritdoc />
    public RiskEvaluation EvaluateRisk(RiskEvaluationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var actionPolicy = WorkflowActionPolicies.GetRequired(input.RequestedAction);
        var context = new RiskRuleContext(input.Facts);

        var outcomes = Rules
            .Select(rule => rule.Evaluate(context))
            .OfType<RiskRuleOutcome>()
            .ToArray();

        var level = outcomes.Aggregate(
            actionPolicy.BaselineRiskLevel,
            (highest, outcome) => Maximum(highest, outcome.RiskLevel));

        return new RiskEvaluation(
            level,
            RecommendationFor(level),
            [actionPolicy.BaselineRiskReason, .. outcomes.Select(outcome => outcome.Reason)],
            [.. outcomes.Select(outcome => outcome.MissingEvidence).OfType<MissingEvidenceItem>()],
            context.SourceDocumentIdsFor(outcomes.SelectMany(outcome => outcome.CitedFactTypes)),
            RequiresApproval: level is RiskLevel.High);
    }

    private static RiskLevel Maximum(RiskLevel left, RiskLevel right) =>
        (RiskLevel)Math.Max((int)left, (int)right);

    private static string RecommendationFor(RiskLevel level) => level switch
    {
        RiskLevel.High => "Do not approve yet.",
        RiskLevel.Medium => "Proceed only with standard controls.",
        _ => "No material evidence gaps were identified."
    };
}
