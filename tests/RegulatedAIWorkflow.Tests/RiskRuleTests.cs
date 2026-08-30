using RegulatedAIWorkflow.Core.Application.Risk;
using RegulatedAIWorkflow.Core.Application.Risk.Rules;
using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Risk;

namespace RegulatedAIWorkflow.Tests;

/// <summary>
/// Each policy condition is one class over typed facts, so each can be proved on its own without an
/// orchestrator, a repository, or a corpus. This is the payoff for extracting them.
/// </summary>
public sealed class RiskRuleTests
{
    private static readonly EvidenceFactType[] PaymentVendorWithCompleteEvidence =
    [
        EvidenceFactType.ProcessesPaymentData,
        EvidenceFactType.SecurityEvidenceRequired,
        EvidenceFactType.Soc2Available,
        EvidenceFactType.DataRetentionScheduleAvailable,
        EvidenceFactType.BreachNotificationPresent
    ];

    /// <summary>Scope rules set the floor and cite what made the decision regulated. They name no gap.</summary>
    [Fact]
    public void Evaluate_ScopeRules_RaiseTheFloorAndCiteWithoutNamingAGap()
    {
        var payment = new PaymentDataScopeRule().Evaluate(Context(EvidenceFactType.ProcessesPaymentData));
        var sensitive = new SensitiveDataScopeRule().Evaluate(Context(EvidenceFactType.ContainsSensitiveData));

        payment.ShouldNotBeNull();
        payment.RiskLevel.ShouldBe(RiskLevel.Medium);
        payment.Reason.Code.ShouldBe("PAYMENT_DATA_IN_SCOPE");
        payment.MissingEvidence.ShouldBeNull();
        payment.CitedFactTypes.ShouldBe(
            [EvidenceFactType.ProcessesPaymentData, EvidenceFactType.SecurityEvidenceRequired]);

        sensitive.ShouldNotBeNull();
        sensitive.RiskLevel.ShouldBe(RiskLevel.Medium);
        sensitive.Reason.Code.ShouldBe("SENSITIVE_DATA_IN_SCOPE");
        sensitive.MissingEvidence.ShouldBeNull();
        sensitive.CitedFactTypes.ShouldBe([EvidenceFactType.ContainsSensitiveData]);
    }

    /// <summary>
    /// Payment data and sensitive data are independent scopes. A vendor in both reports both, which the
    /// if/else this replaced could not do.
    /// </summary>
    [Fact]
    public void Evaluate_VendorInBothScopes_ReportsBothRatherThanTheFirstMatch()
    {
        var context = Context(
            EvidenceFactType.ProcessesPaymentData,
            EvidenceFactType.ContainsSensitiveData);

        new PaymentDataScopeRule().Evaluate(context).ShouldNotBeNull();
        new SensitiveDataScopeRule().Evaluate(context).ShouldNotBeNull();
    }

    /// <summary>Every gap rule fires for a payment vendor with nothing on file, and names its own gap.</summary>
    [Theory]
    [InlineData("security", "SECURITY_REQUIREMENT_UNKNOWN", "APPLICABLE_SECURITY_POLICY")]
    [InlineData("soc2", "SOC2_MISSING", "SOC2_REPORT")]
    [InlineData("retention", "RETENTION_SCHEDULE_MISSING", "DATA_RETENTION_SCHEDULE")]
    [InlineData("breach", "BREACH_NOTIFICATION_MISSING", "BREACH_NOTIFICATION_CLAUSE")]
    public void Evaluate_PaymentVendorWithNoEvidence_FiresWithItsOwnReasonAndGap(
        string ruleKey,
        string expectedReasonCode,
        string expectedGapCode)
    {
        var outcome = RuleFor(ruleKey).Evaluate(Context(EvidenceFactType.ProcessesPaymentData));

        outcome.ShouldNotBeNull();
        outcome.RiskLevel.ShouldBe(RiskLevel.High);
        outcome.Reason.Code.ShouldBe(expectedReasonCode);
        outcome.MissingEvidence.ShouldNotBeNull();
        outcome.MissingEvidence.Code.ShouldBe(expectedGapCode);
    }

    /// <summary>Complete evidence silences every gap rule. Fail-closed must not mean fail-always.</summary>
    [Theory]
    [InlineData("security")]
    [InlineData("soc2")]
    [InlineData("retention")]
    [InlineData("breach")]
    public void Evaluate_PaymentVendorWithCompleteEvidence_DoesNotFire(string ruleKey) =>
        RuleFor(ruleKey).Evaluate(Context(PaymentVendorWithCompleteEvidence)).ShouldBeNull();

    /// <summary>Gap rules apply to payment vendors. A vendor outside that scope is not assessed against them.</summary>
    [Theory]
    [InlineData("security")]
    [InlineData("soc2")]
    [InlineData("retention")]
    [InlineData("breach")]
    public void Evaluate_VendorOutsidePaymentScope_DoesNotFire(string ruleKey) =>
        RuleFor(ruleKey).Evaluate(Context(EvidenceFactType.ContainsSensitiveData)).ShouldBeNull();

    /// <summary>
    /// An explicit "the clause is absent" fact is not cancelled by a "the clause is present" fact
    /// arriving from another document. Contradictory evidence fails closed.
    /// </summary>
    [Fact]
    public void Evaluate_ContradictoryBreachNotificationFacts_FailsClosed()
    {
        var outcome = new MissingBreachNotificationRule().Evaluate(Context(
            EvidenceFactType.ProcessesPaymentData,
            EvidenceFactType.BreachNotificationPresent,
            EvidenceFactType.BreachNotificationMissing));

        outcome.ShouldNotBeNull();
        outcome.Reason.Code.ShouldBe("BREACH_NOTIFICATION_MISSING");
    }

    private static IRiskRule RuleFor(string ruleKey) => ruleKey switch
    {
        "security" => new PaymentSecurityRequirementRule(),
        "soc2" => new MissingSoc2Rule(),
        "retention" => new MissingRetentionScheduleRule(),
        "breach" => new MissingBreachNotificationRule(),
        _ => throw new ArgumentOutOfRangeException(nameof(ruleKey))
    };

    private static RiskRuleContext Context(params EvidenceFactType[] factTypes) =>
        new([.. factTypes.Select(factType => new EvidenceFact($"doc-{factType}", factType))]);
}
