namespace RegulatedAIWorkflow.Core.Domain.Risk;

/// <summary>
/// Captures a deterministic, structured risk assessment.
/// </summary>
/// <param name="RiskLevel">The assessed risk level.</param>
/// <param name="Recommendation">The policy-authored recommended response.</param>
/// <param name="Reasons">The structured reasons supporting the assessment.</param>
/// <param name="CitationReferences">Document identifiers supporting the reasons.</param>
/// <param name="MissingEvidence">Evidence gaps considered by the assessment.</param>
/// <param name="RequiresApproval">Whether an independent approval is required.</param>
/// <param name="EvidenceIsAmbiguous">Whether trustworthy evidence or policy applicability is unresolved.</param>
/// <param name="PolicyVersion">The stable version of the policy that produced the assessment.</param>
public sealed record RiskEvaluation(
    RiskLevel RiskLevel,
    string Recommendation,
    IReadOnlyList<RiskReason> Reasons,
    IReadOnlyList<RiskCitationReference> CitationReferences,
    IReadOnlyList<MissingEvidenceItem> MissingEvidence,
    bool RequiresApproval,
    bool EvidenceIsAmbiguous,
    string PolicyVersion);
