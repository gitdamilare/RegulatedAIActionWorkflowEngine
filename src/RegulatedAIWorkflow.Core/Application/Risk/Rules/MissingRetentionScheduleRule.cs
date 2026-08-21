using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Core.Application.Risk.Rules;

/// <summary>Reports the absence of a retention schedule for a payment-data vendor.</summary>
internal sealed class MissingRetentionScheduleRule : IRiskRule
{
    public RiskRuleOutcome? Evaluate(RiskRuleContext context) =>
        context.ProcessesPaymentData &&
        !context.Has(EvidenceFactType.DataRetentionScheduleAvailable)
            ? new RiskRuleOutcome(
                RiskLevel.High,
                new RiskReason(
                    "RETENTION_SCHEDULE_MISSING",
                    "No data-retention schedule was found."),
                new MissingEvidenceItem(
                    "DATA_RETENTION_SCHEDULE",
                    "Data-retention schedule"),
                EvidenceFactType.SecurityEvidenceRequired)
            : null;
}
