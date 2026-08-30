using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Core.Application.Risk;

/// <summary>
/// The structured contribution of one firing rule. <c>MissingEvidence</c> is nullable because not every
/// finding is a gap: a rule may establish that a decision is regulated without naming anything absent.
/// </summary>
internal sealed record RiskRuleOutcome(
    RiskLevel RiskLevel,
    RiskReason Reason,
    MissingEvidenceItem? MissingEvidence,
    IReadOnlyList<EvidenceFactType> CitedFactTypes);
