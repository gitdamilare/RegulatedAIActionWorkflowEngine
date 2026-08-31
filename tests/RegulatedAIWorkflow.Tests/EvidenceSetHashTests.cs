using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Evidence;

namespace RegulatedAIWorkflow.Tests;

/// <summary>
/// The binding that stops an approval outliving the evidence it was granted against. These tests are the
/// reason the mechanism is defensible in a prototype: a hash nothing can falsify would be decoration.
/// </summary>
public sealed class EvidenceSetHashTests
{
    private static readonly EvidenceQuery Scope = new("northstar-bank", "silverline-payments");

    private static EvidenceDocument Document(
        string documentId = "doc-1",
        EvidenceDocumentType type = EvidenceDocumentType.Contract,
        EvidenceFactType fact = EvidenceFactType.ProcessesPaymentData,
        string snippet = "original text") =>
        new(documentId, Scope.TenantId, Scope.VendorId, type, [fact], UntrustedText.FromExternalSource(snippet));

    private static string Compute(params EvidenceDocument[] documents) =>
        EvidenceSetHash.Compute(Scope, WorkflowAction.MarkVendorApproved, documents);

    [Fact]
    public void Compute_SameDocumentsInDifferentOrder_ProducesTheSameHash()
    {
        var first = Document("doc-a");
        var second = Document("doc-b");

        Compute(first, second).ShouldBe(Compute(second, first));
    }

    [Fact]
    public void Compute_UnchangedEvidence_IsStableAcrossCalls()
    {
        Compute(Document()).ShouldBe(Compute(Document()));
    }

    /// <summary>
    /// Every dimension an approver could reasonably claim to have seen. Any of them moving must change
    /// the binding, or an approval would keep authorizing a decision nobody made.
    /// </summary>
    [Theory]
    [InlineData("document-added")]
    [InlineData("document-removed")]
    [InlineData("document-renamed")]
    [InlineData("document-type-changed")]
    [InlineData("fact-changed")]
    [InlineData("snippet-edited")]
    public void Compute_EvidenceChanged_ProducesADifferentHash(string change)
    {
        var baseline = Compute(Document("doc-a"), Document("doc-b"));

        var mutated = change switch
        {
            "document-added" => Compute(Document("doc-a"), Document("doc-b"), Document("doc-c")),
            "document-removed" => Compute(Document("doc-a")),
            "document-renamed" => Compute(Document("doc-a"), Document("doc-z")),
            "document-type-changed" => Compute(
                Document("doc-a"),
                Document("doc-b", type: EvidenceDocumentType.Policy)),
            "fact-changed" => Compute(
                Document("doc-a"),
                Document("doc-b", fact: EvidenceFactType.Soc2Available)),
            _ => Compute(Document("doc-a"), Document("doc-b", snippet: "quietly replaced text"))
        };

        mutated.ShouldNotBe(baseline);
    }

    /// <summary>
    /// The scope and the action are part of the binding, so an approval cannot be replayed against a
    /// different vendor, a different tenant, or a different action even when the documents match.
    /// </summary>
    [Fact]
    public void Compute_SameDocumentsUnderDifferentScopeOrAction_ProducesDifferentHashes()
    {
        var documents = new[] { Document() };

        var baseline = EvidenceSetHash.Compute(Scope, WorkflowAction.MarkVendorApproved, documents);
        var otherTenant = EvidenceSetHash.Compute(
            new EvidenceQuery("harborview-bank", Scope.VendorId), WorkflowAction.MarkVendorApproved, documents);
        var otherVendor = EvidenceSetHash.Compute(
            new EvidenceQuery(Scope.TenantId, "lakeshore-analytics"), WorkflowAction.MarkVendorApproved, documents);
        var otherAction = EvidenceSetHash.Compute(Scope, WorkflowAction.RequestVendorEvidence, documents);

        new[] { otherTenant, otherVendor, otherAction }.ShouldAllBe(hash => hash != baseline);
    }

    /// <summary>
    /// Length-prefixing exists so field boundaries cannot be forged. Without it a document id ending in a
    /// delimiter could impersonate the start of the next field and two different sets would agree.
    /// </summary>
    [Fact]
    public void Compute_FieldBoundariesShiftedBetweenAdjacentValues_ProducesDifferentHashes()
    {
        var left = Compute(Document("ab", snippet: "shared"), Document("c", snippet: "shared"));
        var right = Compute(Document("a", snippet: "shared"), Document("bc", snippet: "shared"));

        left.ShouldNotBe(right);
    }
}
