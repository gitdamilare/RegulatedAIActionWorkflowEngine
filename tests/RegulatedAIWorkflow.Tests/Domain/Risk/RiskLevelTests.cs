using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Tests.Domain.Risk;

/// <summary>
/// Verifies the domain contract of risk levels.
/// </summary>
public sealed class RiskLevelTests
{
    /// <summary>Protects the numeric ordering used for severity comparisons.</summary>
    [Fact]
    public void RiskLevel_Values_AreOrderedByIncreasingSeverity()
    {
        ((int)RiskLevel.Unknown).ShouldBe(0);
        ((int)RiskLevel.Low).ShouldBe(1);
        ((int)RiskLevel.Medium).ShouldBe(2);
        ((int)RiskLevel.High).ShouldBe(3);
    }
}
