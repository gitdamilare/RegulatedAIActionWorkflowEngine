using System.Reflection;
using RegulatedAIWorkflow.Core.Application;
using RegulatedAIWorkflow.Core.Contracts.Audit;
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
        typeof(IRiskEvaluator).IsAssignableFrom(typeof(DeterministicRiskEvaluator)).ShouldBeTrue();

        var concreteEvaluate = typeof(DeterministicRiskEvaluator)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .ShouldHaveSingleItem();
        concreteEvaluate.Name.ShouldBe(nameof(DeterministicRiskEvaluator.EvaluateRisk));
        concreteEvaluate.GetParameters().ShouldHaveSingleItem().ParameterType
            .ShouldBe(typeof(RiskEvaluationInput));
        typeof(DeterministicRiskEvaluator)
            .GetMember("PolicyVersion", BindingFlags.Public | BindingFlags.Static)
            .ShouldBeEmpty();

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

        var coreAssembly = typeof(DeterministicRiskEvaluator).Assembly;
        var ruleContract = coreAssembly.GetType(
            "RegulatedAIWorkflow.Core.Application.Risk.IRiskRule");
        ruleContract.ShouldNotBeNull();
        ruleContract.IsPublic.ShouldBeFalse();

        var ruleTypes = coreAssembly.GetTypes()
            .Where(type =>
                type is { IsClass: true, IsAbstract: false } &&
                ruleContract.IsAssignableFrom(type))
            .ToArray();
        ruleTypes.ShouldNotBeEmpty();

        foreach (var ruleType in ruleTypes)
        {
            var ruleEvaluate = ruleType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .ShouldHaveSingleItem();
            var ruleInput = ruleEvaluate.GetParameters().ShouldHaveSingleItem().ParameterType;
            var ruleReachableTypes = GetReachableContractTypes(ruleInput);

            ruleReachableTypes.ShouldNotContain(typeof(EvidenceDocument));
            ruleReachableTypes.ShouldNotContain(typeof(UntrustedText));
        }
    }

    /// <summary>
    /// Verifies the audit contract cannot carry request prose, evidence prose, exceptions, or idempotency secrets.
    /// </summary>
    [Fact]
    public void GetProperties_AuditEvent_ContainsOnlySafeStructuredFields()
    {
        var properties = typeof(AuditEvent)
            .GetProperties()
            .ToDictionary(property => property.Name, StringComparer.Ordinal);

        properties.Keys.ShouldNotContain("Question");
        properties.Keys.ShouldNotContain("Snippet");
        properties.Keys.ShouldNotContain("EvidenceSetHash");
        properties.Keys.ShouldNotContain("IdempotencyKey");
        properties.Keys.ShouldNotContain("Exception");

        var reachableTypes = GetReachableContractTypes(typeof(AuditEvent));
        reachableTypes.ShouldNotContain(typeof(WorkflowCommand));
        reachableTypes.ShouldNotContain(typeof(Citation));
        reachableTypes.ShouldNotContain(typeof(EvidenceDocument));
        reachableTypes.ShouldNotContain(typeof(UntrustedText));
        reachableTypes.ShouldNotContain(typeof(Exception));
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
