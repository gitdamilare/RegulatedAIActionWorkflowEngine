using System.Text.RegularExpressions;
using RegulatedAIWorkflow.Core.Domain.Evidence;

namespace RegulatedAIWorkflow.Core.Application.Evidence;

/// <summary>
/// Notices evidence that is trying to issue instructions rather than state facts.
/// <para>
/// This is a detector, not a control, and the distinction is the whole design. Nothing downstream reads
/// its output: it is absent from <see cref="Domain.Risk.RiskEvaluationInput"/>, it cannot change a risk
/// level, and it cannot block an effect. Delete this file and every prompt-injection test still passes,
/// because prose has no route into a decision in the first place. What it adds is visibility, so that a
/// compliance officer can answer "is anyone attempting to manipulate our assessments" — a question the
/// structural control alone leaves unanswered.
/// </para>
/// <para>
/// Keeping it out of the decision path is deliberate rather than lazy. Wire a regex into risk and a false
/// positive raises a compliant vendor's level with no evidence that could ever discharge it, which is the
/// same trap as a blanket "regulated data" floor rule.
/// </para>
/// <para>
/// It reads <see cref="UntrustedText.ForDisplay"/>, the single sanctioned exit, rather than adding a
/// second one. The cost is real and worth stating: content past the display bound is not scanned. That
/// weakens the detector, never the control.
/// </para>
/// </summary>
public static partial class InjectionScanner
{
    private static readonly (string Code, Regex Pattern)[] Rules =
    [
        ("INJECTION_INSTRUCTION_OVERRIDE", InstructionOverride()),
        ("INJECTION_AUTHORITY_CLAIM", AuthorityClaim()),
        ("INJECTION_CONTROL_DIRECTIVE", ControlDirective()),
        ("INJECTION_EVIDENCE_WAIVER", EvidenceWaiver())
    ];

    /// <summary>Returns one note per document that matched a rule. First match wins; order is retrieval order.</summary>
    public static IReadOnlyList<QuarantineNote> Scan(IReadOnlyList<EvidenceDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var notes = new List<QuarantineNote>();

        foreach (var document in documents)
        {
            var text = document.UntrustedSnippet.ForDisplay();

            foreach (var (code, pattern) in Rules)
            {
                if (pattern.IsMatch(text))
                {
                    notes.Add(new QuarantineNote(
                        document.DocumentId,
                        code,
                        document.UntrustedSnippet.Fingerprint()[..16]));
                    break;
                }
            }
        }

        return notes;
    }

    [GeneratedRegex(
        @"\b(ignore|disregard|forget|override)\b.{0,40}\b(previous|prior|above|earlier|all)?\s*(instruction|instructions|prompt|rules|policy|guidance)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 250)]
    private static partial Regex InstructionOverride();

    [GeneratedRegex(
        @"\b(pre-?authori[sz](ed|ation)|pre-?approv(ed|al)|already (approved|authori[sz]ed)|no (further )?(review|approval) (is )?(required|needed))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 250)]
    private static partial Regex AuthorityClaim();

    [GeneratedRegex(
        @"\b(you (are|must|should|may) now|mark the risk as|set risk to|proceed without approval|approve this vendor|do not (log|report|audit))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 250)]
    private static partial Regex ControlDirective();

    [GeneratedRegex(
        @"\bno\s+(soc\s?2|attestation|evidence|report|dpa|assessment)\b.{0,30}\b(required|needed|necessary)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 250)]
    private static partial Regex EvidenceWaiver();
}
