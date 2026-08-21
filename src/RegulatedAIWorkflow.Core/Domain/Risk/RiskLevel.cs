namespace RegulatedAIWorkflow.Core.Domain.Risk;

/// <summary>
/// Describes the risk assigned by deterministic policy.
/// Numeric values are ordered by increasing severity and must remain
/// <see cref="Unknown"/> &lt; <see cref="Low"/> &lt; <see cref="Medium"/> &lt; <see cref="High"/>.
/// </summary>
public enum RiskLevel
{
    /// <summary>The risk has not been evaluated.</summary>
    Unknown = 0,

    /// <summary>The available facts support a low-risk assessment.</summary>
    Low = 1,

    /// <summary>The available facts support a medium-risk assessment.</summary>
    Medium = 2,

    /// <summary>The available facts require a high-risk assessment.</summary>
    High = 3
}
