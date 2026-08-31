using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Core.Application.Risk.Rules;

/// <summary>
/// Reports a missing or explicitly absent breach-notification clause. Silence and an explicit absence are
/// treated the same way: neither is evidence that the clause exists.
/// </summary>
internal sealed class MissingBreachNotificationRule : IRiskRule
{
    public RiskRuleOutcome? Evaluate(RiskRuleContext context) =>
        context.ProcessesPaymentData &&
        (!context.Has(EvidenceFactType.BreachNotificationPresent) ||
         context.Has(EvidenceFactType.BreachNotificationMissing))
            ? new RiskRuleOutcome(
                RiskLevel.High,
                new RiskReason(
                    "BREACH_NOTIFICATION_MISSING",
                    "The contract lacks required breach-notification language."),
                new MissingEvidenceItem(
                    "BREACH_NOTIFICATION_CLAUSE",
                    "Contractual breach-notification clause"),
                [EvidenceFactType.BreachNotificationMissing])
            : null;
}
