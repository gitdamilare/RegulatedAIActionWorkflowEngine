using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Ports;
using RegulatedAIWorkflow.Infrastructure.Evidence;

namespace RegulatedAIWorkflow.Tests.Architecture;

/// <summary>
/// Verifies outbound adapters remain outside Core and independent of the API host.
/// </summary>
public sealed class InfrastructureBoundaryTests
{
    /// <summary>
    /// Verifies Core does not depend on either outer application assembly.
    /// </summary>
    [Fact]
    public void GetReferencedAssemblies_CoreAssembly_DoesNotContainInfrastructureReference()
    {
        var references = typeof(WorkflowCommand).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        references.ShouldNotContain("RegulatedAIWorkflow.Infrastructure");
    }

    /// <summary>
    /// Verifies Infrastructure depends inward on Core without acquiring host dependencies.
    /// </summary>
    [Fact]
    public void GetReferencedAssemblies_InfrastructureAssembly_DependsOnlyInward()
    {
        var references = typeof(InMemoryEvidenceRepository).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        references.ShouldContain("RegulatedAIWorkflow.Core");
        references.ShouldNotContain("RegulatedAIWorkflow.Api");
        references.ShouldNotContain(name =>
            name != null && name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        typeof(IEvidenceRepository).IsAssignableFrom(typeof(InMemoryEvidenceRepository)).ShouldBeTrue();
    }
}
