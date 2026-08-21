using RegulatedAIWorkflow.Core.Application.Risk;
using RegulatedAIWorkflow.Core.Domain.Risk;
using RegulatedAIWorkflow.Core.Ports;

namespace RegulatedAIWorkflow.Core.Application;

/// <summary>
/// Provides a deterministic risk evaluation engine that applies a stable ordered rule set to an evaluation request.
/// </summary>
public sealed class DeterministicRiskEvaluator : IRiskEvaluator
{
    private readonly RiskPolicyDefinition policy;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeterministicRiskEvaluator"/> class with the current selected policy.
    /// </summary>
    public DeterministicRiskEvaluator()
        : this(RiskPolicies.Current)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeterministicRiskEvaluator"/> class with the specified policy.
    /// </summary>
    /// <param name="policy">The risk policy definition to use.</param>
    internal DeterministicRiskEvaluator(RiskPolicyDefinition policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        this.policy = policy;
    }

    /// <inheritdoc />
    public RiskEvaluation EvaluateRisk(RiskEvaluationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var context = new RiskRuleContext(input);
        var outcomes = new List<RiskRuleOutcome>();

        foreach (var rule in policy.Rules)
        {
            var outcome = rule.Evaluate(context);
            if (outcome is null)
            {
                continue;
            }

            outcomes.Add(outcome);
            if (outcome.IsTerminal)
            {
                return CreateEvaluation(context, outcomes, outcome.RiskLevel);
            }
        }

        var riskLevel = outcomes.Aggregate(
            BaselineRiskLevel(context),
            (current, outcome) =>
                (RiskLevel)Math.Max((int)current, (int)outcome.RiskLevel));

        return CreateEvaluation(context, outcomes, riskLevel);
    }

    private RiskEvaluation CreateEvaluation(
        RiskRuleContext context,
        IReadOnlyList<RiskRuleOutcome> outcomes,
        RiskLevel riskLevel) =>
        new(
            riskLevel,
            RecommendationFor(riskLevel),
            outcomes.Select(outcome => outcome.Reason).ToArray(),
            RiskCitationReferenceBuilder.Build(context.Facts, outcomes),
            outcomes.Select(outcome => outcome.MissingEvidence).ToArray(),
            RequiresApproval: riskLevel is RiskLevel.High,
            EvidenceIsAmbiguous: outcomes.Any(outcome => outcome.EvidenceIsAmbiguous),
            policy.Version);

    private static RiskLevel BaselineRiskLevel(RiskRuleContext context) =>
        context.ProcessesPaymentData || context.ContainsSensitiveData
            ? RiskLevel.Medium
            : RiskLevel.Low;

    private static string RecommendationFor(RiskLevel riskLevel) => riskLevel switch
    {
        RiskLevel.High => "Do not approve yet.",
        RiskLevel.Medium => "Proceed only with standard controls.",
        _ => "No material evidence gaps were identified."
    };
}
