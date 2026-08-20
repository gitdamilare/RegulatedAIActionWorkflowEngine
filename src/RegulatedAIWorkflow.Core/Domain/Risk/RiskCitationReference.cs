namespace RegulatedAIWorkflow.Core.Domain.Risk;

/// <summary>
/// Identifies a supporting document without exposing its prose to risk policy.
/// </summary>
/// <param name="DocumentId">The supporting evidence document identifier.</param>
public sealed record RiskCitationReference(string DocumentId);
