using System.Security.Cryptography;
using System.Text;

namespace RegulatedAIWorkflow.Core.Domain.Evidence;

/// <summary>
/// Contains external prose that must not be treated as policy-authoritative input.
/// </summary>
public readonly record struct UntrustedText
{
    private readonly string? value;

    private UntrustedText(string value) => this.value = value;

    /// <summary>
    /// Gets the number of characters in the original external value.
    /// </summary>
    public int Length => value?.Length ?? 0;

    /// <summary>
    /// Explicitly marks text received from an external source as untrusted.
    /// </summary>
    /// <param name="value">The external text, or <see langword="null"/> for an empty value.</param>
    /// <returns>An immutable untrusted-text value.</returns>
    public static UntrustedText FromExternalSource(string? value) => new(value ?? string.Empty);

    /// <summary>
    /// Produces bounded display text with control characters replaced by spaces.
    /// </summary>
    /// <param name="maximumLength">The maximum number of original characters to display.</param>
    /// <returns>Sanitized text, with an ellipsis when the original value was truncated.</returns>
    public string ForDisplay(int maximumLength = 300)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumLength, 1);

        var source = value ?? string.Empty;
        var builder = new StringBuilder(Math.Min(source.Length, maximumLength) + 1);

        foreach (var character in source)
        {
            if (builder.Length >= maximumLength)
            {
                builder.Append('…');
                break;
            }

            builder.Append(char.IsControl(character) ? ' ' : character);
        }

        return builder.ToString().Trim();
    }

    /// <summary>
    /// Computes a stable SHA-256 fingerprint without revealing the source text.
    /// </summary>
    /// <returns>The lowercase hexadecimal content fingerprint.</returns>
    public string Fingerprint() =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty)));

    /// <summary>
    /// Produces a redacted marker suitable for accidental logging.
    /// </summary>
    /// <returns>A marker containing only length and a shortened fingerprint.</returns>
    public override string ToString() => $"[untrusted:{Length}chars:{Fingerprint()[..16]}]";
}
