namespace RegulatedAIWorkflow.Core.Domain.Risk;

/// <summary>
/// Describes the risk assigned by deterministic policy.
/// </summary>
public enum RiskLevel
{
    /// <summary>The risk has not been evaluated.</summary>
    Unknown,

    /// <summary>The available facts support a low-risk assessment.</summary>
    Low,

    /// <summary>The available facts support a medium-risk assessment.</summary>
    Medium,

    /// <summary>The available facts require a high-risk assessment.</summary>
    High
}
