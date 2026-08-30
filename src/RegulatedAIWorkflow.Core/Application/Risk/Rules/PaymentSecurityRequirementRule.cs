using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Core.Application.Risk.Rules;

/// <summary>
/// Fails closed when payment data is processed but no applicable security requirement was established.
/// An unknown requirement raises risk; it never lowers it. Nothing is cited, because the absence of a
/// policy document is precisely what there is no document for.
/// </summary>
internal sealed class PaymentSecurityRequirementRule : IRiskRule
{
    public RiskRuleOutcome? Evaluate(RiskRuleContext context) =>
        context.ProcessesPaymentData &&
        !context.Has(EvidenceFactType.SecurityEvidenceRequired)
            ? new RiskRuleOutcome(
                RiskLevel.High,
                new RiskReason(
                    "SECURITY_REQUIREMENT_UNKNOWN",
                    "The applicable payment-data security requirement could not be established."),
                new MissingEvidenceItem(
                    "APPLICABLE_SECURITY_POLICY",
                    "Applicable payment-data security policy"),
                [])
            : null;
}
