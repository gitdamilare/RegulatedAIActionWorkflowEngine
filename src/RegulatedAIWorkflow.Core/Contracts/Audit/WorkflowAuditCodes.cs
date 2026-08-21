namespace RegulatedAIWorkflow.Core.Contracts.Audit;

/// <summary>
/// Provides the stable workflow-level reason codes written to audit events.
/// </summary>
public static class WorkflowAuditCodes
{
    /// <summary>The request failed bounded validation.</summary>
    public const string InvalidRequest = "INVALID_REQUEST";

    /// <summary>The caller's role may not attempt the requested action.</summary>
    public const string RoleNotAuthorized = "ROLE_NOT_AUTHORIZED";

    /// <summary>Retrieved evidence violated the requested tenant or vendor scope.</summary>
    public const string EvidenceScopeViolation = "EVIDENCE_SCOPE_VIOLATION";

    /// <summary>One or more assessment citations could not be verified.</summary>
    public const string CitationVerificationFailed = "CITATION_VERIFICATION_FAILED";

    /// <summary>Scoped evidence was empty or the assessment declared it ambiguous.</summary>
    public const string EvidenceGateFailed = "EVIDENCE_GATE_FAILED";
}
