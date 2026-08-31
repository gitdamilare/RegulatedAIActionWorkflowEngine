namespace RegulatedAIWorkflow.Core.Domain.Risk;

/// <summary>A deterministic, structured risk assessment produced from typed facts alone.</summary>
public sealed record RiskEvaluation(
    RiskLevel RiskLevel,
    string Recommendation,
    IReadOnlyList<RiskReason> Reasons,
    IReadOnlyList<MissingEvidenceItem> MissingEvidence,
    IReadOnlyList<string> CitedDocumentIds,
    bool RequiresApproval);

/// <summary>A structured reason supporting an assessment. Codes are server-owned; text is never caller-supplied.</summary>
public sealed record RiskReason(string Code, string Message);

/// <summary>An evidence gap the assessment counted against the vendor.</summary>
public sealed record MissingEvidenceItem(string Code, string Description);
