namespace RegulatedAIWorkflow.Core.Domain.Risk;

/// <summary>
/// Describes evidence required for a trustworthy decision but not found in the fact set.
/// </summary>
/// <param name="Code">The machine-readable missing-evidence code.</param>
/// <param name="Description">The deterministic, policy-authored description.</param>
public sealed record MissingEvidenceItem(string Code, string Description);
