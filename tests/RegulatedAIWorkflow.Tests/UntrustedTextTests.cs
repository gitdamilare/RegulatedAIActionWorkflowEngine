using System.Reflection;
using RegulatedAIWorkflow.Core.Domain.Evidence;

namespace RegulatedAIWorkflow.Tests;

/// <summary>
/// Verifies the construction and safe-handling behavior of <see cref="UntrustedText"/>.
/// </summary>
public sealed class UntrustedTextTests
{
    /// <summary>
    /// Verifies that raw strings require the named untrusted-text factory.
    /// </summary>
    [Fact]
    public void FromExternalSource_StringConstruction_IsOnlyPublicRawTextEntryPoint()
    {
        var publicStringConstructors = typeof(UntrustedText)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(constructor => constructor.GetParameters().Any(parameter => parameter.ParameterType == typeof(string)));

        var stringConversions = typeof(UntrustedText)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name is "op_Implicit" or "op_Explicit")
            .Where(method => method.ReturnType == typeof(string) ||
                method.GetParameters().Any(parameter => parameter.ParameterType == typeof(string)));

        publicStringConstructors.ShouldBeEmpty();
        stringConversions.ShouldBeEmpty();
        typeof(UntrustedText).GetMethod(nameof(UntrustedText.FromExternalSource)).ShouldNotBeNull();
    }

    /// <summary>
    /// Verifies that display text is sanitized and bounded.
    /// </summary>
    [Fact]
    public void ForDisplay_ControlCharactersAndLongText_ReturnsSanitizedTruncatedText()
    {
        const string rawText = "secret\u001bcontent that is longer than ten";
        var text = UntrustedText.FromExternalSource(rawText);

        text.ForDisplay(10).ShouldBe("secret con…");
        text.ForDisplay().ShouldNotContain('\u001b');
    }

    /// <summary>
    /// Verifies that accidental string conversion never exposes untrusted prose.
    /// </summary>
    [Fact]
    public void ToString_UntrustedText_IsRedactedForAccidentalLogging()
    {
        var text = UntrustedText.FromExternalSource("secret content");

        text.ToString().ShouldNotContain("secret");
        text.ToString().ShouldNotContain("content");
    }

    /// <summary>
    /// Verifies that fingerprints are stable and sensitive to content changes.
    /// </summary>
    [Fact]
    public void Fingerprint_SameAndDifferentContent_IsStableAndContentSensitive()
    {
        const string rawText = "secret content";
        var fingerprint = UntrustedText.FromExternalSource(rawText).Fingerprint();

        fingerprint.ShouldMatch("^[0-9a-f]{64}$");
        UntrustedText.FromExternalSource(rawText).Fingerprint().ShouldBe(fingerprint);
        UntrustedText.FromExternalSource($"{rawText}!").Fingerprint().ShouldNotBe(fingerprint);
    }
}
