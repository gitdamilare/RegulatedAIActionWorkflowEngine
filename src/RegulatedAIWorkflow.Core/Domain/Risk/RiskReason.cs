namespace RegulatedAIWorkflow.Core.Domain.Risk;

/// <summary>
/// Provides a stable reason code and safe policy-authored explanation.
/// </summary>
/// <param name="Code">The machine-readable reason code.</param>
/// <param name="Message">The deterministic, policy-authored explanation.</param>
public sealed record RiskReason(string Code, string Message);
