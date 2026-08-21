using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Core.Application.Risk;

/// <summary>
/// Describes the structured contribution made by one firing policy rule.
/// </summary>
internal sealed record RiskRuleOutcome(
    RiskLevel RiskLevel,
    RiskReason Reason,
    MissingEvidenceItem MissingEvidence,
    EvidenceFactType? CitationSourceFactType,
    bool EvidenceIsAmbiguous = false,
    bool IsTerminal = false);
