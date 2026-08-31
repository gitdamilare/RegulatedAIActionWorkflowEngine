namespace RegulatedAIWorkflow.Core.Domain.Evidence;

/// <summary>
/// A record that one document tried to issue instructions rather than state facts. Every field is
/// server-owned: the document it came from, the rule that matched, and a fingerprint of the content. The
/// matched text is deliberately absent, so an investigator can prove which content this was without the
/// audit trail becoming a place the payload is stored.
/// </summary>
public sealed record QuarantineNote(string DocumentId, string RuleCode, string ContentFingerprint);
