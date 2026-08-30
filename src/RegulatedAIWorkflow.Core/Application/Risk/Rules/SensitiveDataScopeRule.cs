using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Core.Application.Risk.Rules;

/// <summary>
/// Establishes that sensitive data puts the decision in regulated scope. Independent of payment data:
/// a vendor may be in scope for both, and both are then reported.
/// </summary>
internal sealed class SensitiveDataScopeRule : IRiskRule
{
    public RiskRuleOutcome? Evaluate(RiskRuleContext context) =>
        context.ContainsSensitiveData
            ? new RiskRuleOutcome(
                RiskLevel.Medium,
                new RiskReason(
                    "SENSITIVE_DATA_IN_SCOPE",
                    "The vendor handles sensitive data, so the decision is subject to data-protection controls."),
                MissingEvidence: null,
                [EvidenceFactType.ContainsSensitiveData])
            : null;
}
