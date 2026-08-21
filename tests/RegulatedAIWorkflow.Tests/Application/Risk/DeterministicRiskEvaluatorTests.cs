using System.Text.Json;
using RegulatedAIWorkflow.Core.Application;
using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Risk;
using RegulatedAIWorkflow.Infrastructure.Evidence;

namespace RegulatedAIWorkflow.Tests.Application.Risk;

/// <summary>
/// Verifies the versioned risk policy produces stable decisions from typed facts only.
/// </summary>
public sealed class DeterministicRiskEvaluatorTests
{
    private readonly DeterministicRiskEvaluator evaluator = new();

    /// <summary>
    /// Verifies the Northstar and Silverline fixture fails high for every known control gap.
    /// </summary>
    [Fact]
    public async Task EvaluateRisk_NorthstarSilverlineEvidence_ReturnsExpectedHighRiskDecision()
    {
        const string tenantId = "northstar-bank";
        const string vendorId = "silverline-payments";
        var repository = new InMemoryEvidenceRepository();
        var retrieved = await repository.SearchEvidenceAsync(
            new EvidenceQuery(tenantId, vendorId),
            CancellationToken.None);
        var scoped = EvidenceSecurity.EnforceScope(retrieved, tenantId, vendorId);

        var result = evaluator.EvaluateRisk(new RiskEvaluationInput(
            scoped.Evidence.Facts,
            HasScopedEvidence: scoped.Evidence.Documents.Count > 0));

        result.RiskLevel.ShouldBe(RiskLevel.High);
        result.Recommendation.ShouldBe("Do not approve yet.");
        result.RequiresApproval.ShouldBeTrue();
        result.EvidenceIsAmbiguous.ShouldBeFalse();
        result.Reasons.Select(reason => reason.Code).ShouldBe(
            ["SOC2_MISSING", "RETENTION_SCHEDULE_MISSING", "BREACH_NOTIFICATION_MISSING"]);
        result.MissingEvidence.Select(item => item.Code).ShouldBe(
            ["SOC2_REPORT", "DATA_RETENTION_SCHEDULE", "BREACH_NOTIFICATION_CLAUSE"]);
        result.CitationReferences.Select(reference => reference.DocumentId).ShouldBe(
            ["northstar-policy-002", "northstar-silverline-contract"]);
        result.PolicyVersion.ShouldBe("risk-2026.08.1");
    }

    /// <summary>Verifies the SOC 2 rule fires independently and cites the policy requirement.</summary>
    [Fact]
    public void EvaluateRisk_MissingSoc2_ReturnsOnlySoc2Gap()
    {
        var input = Input(
            Fact(EvidenceFactType.ProcessesPaymentData, "payment-source"),
            Fact(EvidenceFactType.SecurityEvidenceRequired, "policy-source"),
            Fact(EvidenceFactType.DataRetentionScheduleAvailable, "retention-source"),
            Fact(EvidenceFactType.BreachNotificationPresent, "contract-source"));

        var result = evaluator.EvaluateRisk(input);

        result.Reasons.Select(reason => reason.Code).ShouldBe(["SOC2_MISSING"]);
        result.MissingEvidence.Select(item => item.Code).ShouldBe(["SOC2_REPORT"]);
        result.CitationReferences.Select(reference => reference.DocumentId).ShouldBe(["policy-source"]);
    }

    /// <summary>Verifies the retention rule fires independently and cites the policy requirement.</summary>
    [Fact]
    public void EvaluateRisk_MissingRetentionSchedule_ReturnsOnlyRetentionGap()
    {
        var input = Input(
            Fact(EvidenceFactType.ProcessesPaymentData, "payment-source"),
            Fact(EvidenceFactType.SecurityEvidenceRequired, "policy-source"),
            Fact(EvidenceFactType.Soc2Available, "soc2-source"),
            Fact(EvidenceFactType.BreachNotificationPresent, "contract-source"));

        var result = evaluator.EvaluateRisk(input);

        result.Reasons.Select(reason => reason.Code).ShouldBe(["RETENTION_SCHEDULE_MISSING"]);
        result.MissingEvidence.Select(item => item.Code).ShouldBe(["DATA_RETENTION_SCHEDULE"]);
        result.CitationReferences.Select(reference => reference.DocumentId).ShouldBe(["policy-source"]);
    }

    /// <summary>Verifies an explicit breach gap cites the contract that reports it.</summary>
    [Fact]
    public void EvaluateRisk_ExplicitlyMissingBreachNotification_CitesItsSource()
    {
        var input = PaymentInputWithSoc2AndRetention(
            Fact(EvidenceFactType.BreachNotificationMissing, "contract-source"));

        var result = evaluator.EvaluateRisk(input);

        result.Reasons.Select(reason => reason.Code).ShouldBe(["BREACH_NOTIFICATION_MISSING"]);
        result.MissingEvidence.Select(item => item.Code).ShouldBe(["BREACH_NOTIFICATION_CLAUSE"]);
        result.CitationReferences.Select(reference => reference.DocumentId).ShouldBe(["contract-source"]);
    }

    /// <summary>Verifies absence can fail high without manufacturing an unsupported citation.</summary>
    [Fact]
    public void EvaluateRisk_UnsupportedBreachNotificationAbsence_DoesNotInventCitation()
    {
        var result = evaluator.EvaluateRisk(PaymentInputWithSoc2AndRetention());

        result.Reasons.Select(reason => reason.Code).ShouldBe(["BREACH_NOTIFICATION_MISSING"]);
        result.CitationReferences.ShouldBeEmpty();
    }

    /// <summary>Verifies the absence of trustworthy scoped evidence fails high.</summary>
    [Fact]
    public void EvaluateRisk_NoScopedEvidence_ReturnsAmbiguousHighRiskDecision()
    {
        var result = evaluator.EvaluateRisk(new RiskEvaluationInput([], HasScopedEvidence: false));

        AssertAmbiguousEvidenceResult(result);
        result.Reasons.ShouldHaveSingleItem().Message.ShouldBe(
            "No trustworthy tenant-scoped evidence was available for the decision.");
        result.CitationReferences.ShouldBeEmpty();
    }

    /// <summary>Verifies payment processing without an applicable requirement fails high.</summary>
    [Fact]
    public void EvaluateRisk_UnknownPaymentSecurityRequirement_ReturnsAmbiguousHighRiskDecision()
    {
        var result = evaluator.EvaluateRisk(Input(
            Fact(EvidenceFactType.ProcessesPaymentData, "payment-source")));

        AssertAmbiguousEvidenceResult(result);
        result.Reasons.ShouldHaveSingleItem().Message.ShouldBe(
            "The applicable payment-data security requirement is unknown.");
        result.CitationReferences.ShouldBeEmpty();
    }

    /// <summary>Verifies complete payment controls produce the stable medium-risk floor.</summary>
    [Fact]
    public void EvaluateRisk_CompletePaymentEvidence_ReturnsMediumWithoutGaps()
    {
        var result = evaluator.EvaluateRisk(PaymentInputWithSoc2AndRetention(
            Fact(EvidenceFactType.BreachNotificationPresent, "contract-source")));

        result.RiskLevel.ShouldBe(RiskLevel.Medium);
        result.Recommendation.ShouldBe("Proceed only with standard controls.");
        result.Reasons.ShouldBeEmpty();
        result.MissingEvidence.ShouldBeEmpty();
        result.CitationReferences.ShouldBeEmpty();
        result.RequiresApproval.ShouldBeFalse();
        result.EvidenceIsAmbiguous.ShouldBeFalse();
    }

    /// <summary>Verifies data sensitivity establishes the medium and low classification floors.</summary>
    [Theory]
    [InlineData(EvidenceFactType.ContainsSensitiveData, RiskLevel.Medium)]
    [InlineData(EvidenceFactType.SecurityEvidenceRequired, RiskLevel.Low)]
    public void EvaluateRisk_NonPaymentEvidence_ReturnsExpectedRiskLevel(
        EvidenceFactType factType,
        RiskLevel expectedRiskLevel)
    {
        var result = evaluator.EvaluateRisk(Input(Fact(factType, "source-document")));

        result.RiskLevel.ShouldBe(expectedRiskLevel);
        result.Reasons.ShouldBeEmpty();
        result.MissingEvidence.ShouldBeEmpty();
        result.RequiresApproval.ShouldBeFalse();
    }

    /// <summary>Verifies supporting identifiers are sorted and deduplicated in rule order.</summary>
    [Fact]
    public void EvaluateRisk_DuplicateSupportingFacts_ReturnsCanonicalFactBoundCitations()
    {
        var input = Input(
            Fact(EvidenceFactType.ProcessesPaymentData, "payment-source"),
            Fact(EvidenceFactType.SecurityEvidenceRequired, "z-policy"),
            Fact(EvidenceFactType.SecurityEvidenceRequired, "a-policy"),
            Fact(EvidenceFactType.SecurityEvidenceRequired, "a-policy"),
            Fact(EvidenceFactType.BreachNotificationMissing, "z-contract"),
            Fact(EvidenceFactType.BreachNotificationMissing, "b-contract"));

        var result = evaluator.EvaluateRisk(input);

        result.CitationReferences.Select(reference => reference.DocumentId).ShouldBe(
            ["a-policy", "z-policy", "b-contract", "z-contract"]);
        var inputSourceIds = input.Facts
            .Select(fact => fact.SourceDocumentId)
            .ToHashSet(StringComparer.Ordinal);
        result.CitationReferences.ShouldAllBe(reference => inputSourceIds.Contains(reference.DocumentId));
    }

    /// <summary>Verifies policy version and complete output remain stable for identical input.</summary>
    [Fact]
    public void EvaluateRisk_IdenticalInput_ReturnsIdenticalVersionedAssessment()
    {
        var input = Input(
            Fact(EvidenceFactType.ProcessesPaymentData, "payment-source"),
            Fact(EvidenceFactType.SecurityEvidenceRequired, "policy-source"),
            Fact(EvidenceFactType.BreachNotificationMissing, "contract-source"));

        var first = evaluator.EvaluateRisk(input);
        var second = evaluator.EvaluateRisk(input);

        first.PolicyVersion.ShouldBe("risk-2026.08.1");
        JsonSerializer.Serialize(second).ShouldBe(JsonSerializer.Serialize(first));
    }

    /// <summary>Verifies a missing input is rejected rather than silently assessed.</summary>
    [Fact]
    public void EvaluateRisk_NullInput_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => evaluator.EvaluateRisk(null!));
    }

    private static RiskEvaluationInput PaymentInputWithSoc2AndRetention(params EvidenceFact[] breachFacts) =>
        Input(
            [
                Fact(EvidenceFactType.ProcessesPaymentData, "payment-source"),
                Fact(EvidenceFactType.SecurityEvidenceRequired, "policy-source"),
                Fact(EvidenceFactType.Soc2Available, "soc2-source"),
                Fact(EvidenceFactType.DataRetentionScheduleAvailable, "retention-source"),
                .. breachFacts
            ]);

    private static RiskEvaluationInput Input(params EvidenceFact[] facts) =>
        new(facts, HasScopedEvidence: true);

    private static EvidenceFact Fact(EvidenceFactType factType, string sourceDocumentId) =>
        new("northstar-bank", "silverline-payments", sourceDocumentId, factType);

    private static void AssertAmbiguousEvidenceResult(RiskEvaluation result)
    {
        result.RiskLevel.ShouldBe(RiskLevel.High);
        result.Recommendation.ShouldBe("Do not approve yet.");
        result.RequiresApproval.ShouldBeTrue();
        result.EvidenceIsAmbiguous.ShouldBeTrue();
        result.Reasons.ShouldHaveSingleItem().Code.ShouldBe("EVIDENCE_AMBIGUOUS");
        result.MissingEvidence.ShouldHaveSingleItem().Code.ShouldBe("TRUSTWORTHY_EVIDENCE");
        result.PolicyVersion.ShouldBe("risk-2026.08.1");
    }
}
