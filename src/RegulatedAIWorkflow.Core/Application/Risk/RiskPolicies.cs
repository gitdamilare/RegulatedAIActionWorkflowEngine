using RegulatedAIWorkflow.Core.Application.Risk.Rules;

namespace RegulatedAIWorkflow.Core.Application.Risk;

/// <summary>
/// Defines the immutable server-controlled policy versions known to Core.
/// </summary>
internal static class RiskPolicies
{
    /// <summary>The first deterministic policy in its required stable order.</summary>
    internal static RiskPolicyDefinition Version1 { get; } =
        new(
            "risk-2026.08.1",
            [
                new TrustworthyScopedEvidenceRule(),
                new PaymentSecurityRequirementRule(),
                new MissingSoc2Rule(),
                new MissingRetentionScheduleRule(),
                new MissingBreachNotificationRule()
            ]);

    /// <summary>The policy selected by the server for new evaluations.</summary>
    internal static RiskPolicyDefinition Current => Version1;
}
