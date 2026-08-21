using System.Collections.ObjectModel;

namespace RegulatedAIWorkflow.Core.Application.Risk;

/// <summary>
/// Binds an immutable ordered rule set to the version recorded on its decisions.
/// </summary>
internal sealed class RiskPolicyDefinition
{
    public RiskPolicyDefinition(string version, IEnumerable<IRiskRule> rules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(rules);

        var ruleArray = rules.ToArray();
        if (ruleArray.Any(rule => rule is null))
        {
            throw new ArgumentException("A policy cannot contain a null rule.", nameof(rules));
        }

        Version = version;
        Rules = Array.AsReadOnly(ruleArray);
    }

    /// <summary>The stable version recorded on every evaluation.</summary>
    public string Version { get; }

    /// <summary>The rules in deterministic execution order.</summary>
    public ReadOnlyCollection<IRiskRule> Rules { get; }
}
