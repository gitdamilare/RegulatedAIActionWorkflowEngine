using RegulatedAIWorkflow.Core.Application.Approval;
using RegulatedAIWorkflow.Core.Domain.Evidence;

namespace RegulatedAIWorkflow.Tests.Application.Approval;

/// <summary>
/// Verifies the evidence binding is canonical and invalidated by material changes.
/// </summary>
public sealed class CanonicalEvidenceHasherTests
{
    /// <summary>Equivalent collection order produces the same binding.</summary>
    [Fact]
    public void Compute_ReorderedEvidence_ReturnsSameHash()
    {
        var evidence = Evidence();
        var reordered = new EvidenceSearchResult(
            evidence.Documents.Reverse().ToArray(),
            evidence.Facts.Reverse().ToArray());

        var expected = Compute(evidence);
        var actual = Compute(reordered);

        actual.ShouldBe(expected);
        actual.Length.ShouldBe(64);
    }

    /// <summary>Every approval-significant evidence component affects the binding.</summary>
    [Fact]
    public void Compute_ChangedBindingField_ReturnsDifferentHash()
    {
        var evidence = Evidence();
        var original = Compute(evidence);
        var changedDocument = evidence.Documents[0] with
        {
            UntrustedSnippet = UntrustedText.FromExternalSource("changed content")
        };
        var changedFact = evidence.Facts[0] with
        {
            FactType = EvidenceFactType.Soc2Available
        };

        var changedHashes = new[]
        {
            Compute(new EvidenceSearchResult(
                [changedDocument, evidence.Documents[1]],
                evidence.Facts)),
            Compute(new EvidenceSearchResult(
                [evidence.Documents[0] with { DocumentId = "changed-id" }, evidence.Documents[1]],
                evidence.Facts)),
            Compute(new EvidenceSearchResult(
                evidence.Documents,
                [changedFact, evidence.Facts[1]])),
            CanonicalEvidenceHasher.Compute("other-tenant", "vendor", evidence, "policy-1"),
            CanonicalEvidenceHasher.Compute("tenant", "other-vendor", evidence, "policy-1"),
            CanonicalEvidenceHasher.Compute("tenant", "vendor", evidence, "policy-2")
        };

        changedHashes.ShouldAllBe(hash => hash != original);
    }

    private static string Compute(EvidenceSearchResult evidence) =>
        CanonicalEvidenceHasher.Compute("tenant", "vendor", evidence, "policy-1");

    private static EvidenceSearchResult Evidence() =>
        new(
            [
                WorkflowTestHarness.Document("document-a", "alpha", "tenant", "vendor"),
                WorkflowTestHarness.Document("document-b", "beta", "tenant", "vendor")
            ],
            [
                WorkflowTestHarness.Fact(
                    "document-a",
                    EvidenceFactType.SecurityEvidenceRequired,
                    "tenant",
                    "vendor"),
                WorkflowTestHarness.Fact(
                    "document-b",
                    EvidenceFactType.ProcessesPaymentData,
                    "tenant",
                    "vendor")
            ]);
}
