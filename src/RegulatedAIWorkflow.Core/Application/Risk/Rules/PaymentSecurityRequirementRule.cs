using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Core.Application.Risk.Rules;

/// <summary>Fails closed when payment processing lacks an applicable security requirement.</summary>
internal sealed class PaymentSecurityRequirementRule : IRiskRule
{
    public RiskRuleOutcome? Evaluate(RiskRuleContext context) =>
        context.ProcessesPaymentData &&
        !context.Has(EvidenceFactType.SecurityEvidenceRequired)
            ? new RiskRuleOutcome(
                RiskLevel.High,
                new RiskReason(
                    "EVIDENCE_AMBIGUOUS",
                    "The applicable payment-data security requirement is unknown."),
                new MissingEvidenceItem(
                    "TRUSTWORTHY_EVIDENCE",
                    "Trustworthy tenant-scoped evidence"),
                CitationSourceFactType: null,
                EvidenceIsAmbiguous: true,
                IsTerminal: true)
            : null;
}
