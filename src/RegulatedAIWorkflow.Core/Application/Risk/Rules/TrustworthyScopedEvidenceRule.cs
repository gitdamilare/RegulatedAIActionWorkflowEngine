using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Core.Application.Risk.Rules;

/// <summary>Fails closed when no trustworthy scoped evidence is available.</summary>
internal sealed class TrustworthyScopedEvidenceRule : IRiskRule
{
    public RiskRuleOutcome? Evaluate(RiskRuleContext context) =>
        context.HasScopedEvidence
            ? null
            : new RiskRuleOutcome(
                RiskLevel.High,
                new RiskReason(
                    "EVIDENCE_AMBIGUOUS",
                    "No trustworthy tenant-scoped evidence was available for the decision."),
                new MissingEvidenceItem(
                    "TRUSTWORTHY_EVIDENCE",
                    "Trustworthy tenant-scoped evidence"),
                CitationSourceFactType: null,
                EvidenceIsAmbiguous: true,
                IsTerminal: true);
}
