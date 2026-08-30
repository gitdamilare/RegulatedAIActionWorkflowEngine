using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Core.Application.Risk.Rules;

/// <summary>
/// Establishes that payment data puts the decision in regulated scope. It names no gap; it sets the floor
/// and cites the documents that made this a regulated question in the first place.
/// </summary>
internal sealed class PaymentDataScopeRule : IRiskRule
{
    public RiskRuleOutcome? Evaluate(RiskRuleContext context) =>
        context.ProcessesPaymentData
            ? new RiskRuleOutcome(
                RiskLevel.Medium,
                new RiskReason(
                    "PAYMENT_DATA_IN_SCOPE",
                    "The vendor processes payment data, so the decision is subject to payment-data controls."),
                MissingEvidence: null,
                [EvidenceFactType.ProcessesPaymentData, EvidenceFactType.SecurityEvidenceRequired])
            : null;
}
