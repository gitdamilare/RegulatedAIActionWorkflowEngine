using System.Reflection;
using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Risk;
using RegulatedAIWorkflow.Core.Ports;

namespace RegulatedAIWorkflow.Tests.Architecture;

/// <summary>
/// Verifies the framework and trust boundaries enforced by the Core contracts.
/// </summary>
public sealed class ArchitectureBoundaryTests
{
    /// <summary>
    /// Verifies that Core remains independent of the API and ASP.NET.
    /// </summary>
    [Fact]
    public void GetReferencedAssemblies_CoreAssembly_DoesNotContainApiOrAspNetReferences()
    {
        var references = typeof(WorkflowCommand).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        references.ShouldNotContain("RegulatedAIWorkflow.Api");
        references.ShouldNotContain(name =>
            name != null && name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies that risk evaluation can receive normalized facts but not evidence prose.
    /// </summary>
    [Fact]
    public void EvaluateRisk_RiskInputContract_AcceptsOnlyScopedTypedFacts()
    {
        var evaluate = typeof(IRiskEvaluator).GetMethods().ShouldHaveSingleItem();
        var parameter = evaluate.GetParameters().ShouldHaveSingleItem();

        evaluate.Name.ShouldBe(nameof(IRiskEvaluator.EvaluateRisk));
        parameter.ParameterType.ShouldBe(typeof(RiskEvaluationInput));

        var inputProperties = typeof(RiskEvaluationInput)
            .GetProperties()
            .ToDictionary(property => property.Name, StringComparer.Ordinal);

        inputProperties.Count.ShouldBe(2);
        inputProperties[nameof(RiskEvaluationInput.Facts)].PropertyType
            .ShouldBe(typeof(IReadOnlyList<EvidenceFact>));
        inputProperties[nameof(RiskEvaluationInput.HasScopedEvidence)].PropertyType
            .ShouldBe(typeof(bool));

        var factProperties = typeof(EvidenceFact)
            .GetProperties()
            .ToDictionary(property => property.Name, StringComparer.Ordinal);

        factProperties.Count.ShouldBe(4);
        factProperties[nameof(EvidenceFact.TenantId)].PropertyType.ShouldBe(typeof(string));
        factProperties[nameof(EvidenceFact.VendorId)].PropertyType.ShouldBe(typeof(string));
        factProperties[nameof(EvidenceFact.SourceDocumentId)].PropertyType.ShouldBe(typeof(string));
        factProperties[nameof(EvidenceFact.FactType)].PropertyType.ShouldBe(typeof(EvidenceFactType));

        var reachableTypes = GetReachableContractTypes(parameter.ParameterType);

        reachableTypes.ShouldNotContain(typeof(EvidenceDocument));
        reachableTypes.ShouldNotContain(typeof(UntrustedText));
    }

    private static HashSet<Type> GetReachableContractTypes(Type rootType)
    {
        var coreAssembly = typeof(RiskEvaluationInput).Assembly;
        var pending = new Stack<Type>();
        var visited = new HashSet<Type>();
        pending.Push(rootType);

        while (pending.TryPop(out var current))
        {
            if (!visited.Add(current))
            {
                continue;
            }

            foreach (var argument in current.GetGenericArguments())
            {
                pending.Push(argument);
            }

            if (current.Assembly != coreAssembly)
            {
                continue;
            }

            foreach (var property in current.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                pending.Push(property.PropertyType);
            }
        }

        return visited;
    }
}
