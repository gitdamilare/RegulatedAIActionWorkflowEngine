using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Core.Application.Risk.Rules;

/// <summary>Reports the absence of current SOC 2 evidence for a payment-data vendor.</summary>
internal sealed class MissingSoc2Rule : IRiskRule
{
    public RiskRuleOutcome? Evaluate(RiskRuleContext context) =>
        context.ProcessesPaymentData &&
        !context.Has(EvidenceFactType.Soc2Available)
            ? new RiskRuleOutcome(
                RiskLevel.High,
                new RiskReason("SOC2_MISSING", "No current SOC 2 evidence was found."),
                new MissingEvidenceItem("SOC2_REPORT", "Current SOC 2 report"),
                EvidenceFactType.SecurityEvidenceRequired)
            : null;
}
