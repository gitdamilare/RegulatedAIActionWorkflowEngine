using System.Reflection;
using RegulatedAIWorkflow.Core.Contracts.Audit;
using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Domain.Evidence;
using RegulatedAIWorkflow.Core.Domain.Risk;
using RegulatedAIWorkflow.Core.Ports;

namespace RegulatedAIWorkflow.Tests.Architecture;

/// <summary>
/// The one architecture test worth keeping. Project references already make the dependency direction a
/// compile error, but nothing in the compiler stops someone adding a prose field to the risk input. This
/// fails the moment that field appears, before any code reads it.
/// </summary>
public sealed class RiskInputContractTests
{
    [Fact]
    public void EvaluateRisk_RiskInputContract_AcceptsOnlyActionAndScopedTypedFacts()
    {
        var parameter = typeof(IRiskEvaluator).GetMethods().ShouldHaveSingleItem().GetParameters().ShouldHaveSingleItem();
        parameter.ParameterType.ShouldBe(typeof(RiskEvaluationInput));

        // An added property fails here even if nothing reads it.
        typeof(RiskEvaluationInput).GetProperties().Length.ShouldBe(2);
        typeof(EvidenceFact).GetProperties().Length.ShouldBe(2);

        // Nothing prose-bearing is reachable from the input, or from the audit record.
        var fromRiskInput = Reachable(typeof(RiskEvaluationInput));
        fromRiskInput.ShouldNotContain(typeof(EvidenceDocument));
        fromRiskInput.ShouldNotContain(typeof(Citation));
        fromRiskInput.ShouldNotContain(typeof(WorkflowCommand));

        var fromAuditEvent = Reachable(typeof(AuditEvent));
        fromAuditEvent.ShouldNotContain(typeof(EvidenceDocument));
        fromAuditEvent.ShouldNotContain(typeof(Citation));
        fromAuditEvent.ShouldNotContain(typeof(WorkflowCommand));
    }

    /// <summary>Walks the public property graph, following generic arguments, staying inside Core.</summary>
    private static HashSet<Type> Reachable(Type rootType)
    {
        var coreAssembly = typeof(RiskEvaluationInput).Assembly;
        var pending = new Stack<Type>([rootType]);
        var visited = new HashSet<Type>();

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
