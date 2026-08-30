using System.Collections.Concurrent;
using RegulatedAIWorkflow.Core.Domain.Execution;
using RegulatedAIWorkflow.Core.Ports;

namespace RegulatedAIWorkflow.Infrastructure.Execution;

/// <summary>Stands in for the real effect. Records what would have happened; changes nothing outside the process.</summary>
public sealed class InMemoryActionExecutor : IActionExecutor
{
    private readonly ConcurrentQueue<ActionExecutionRequest> executions = new();

    /// <summary>A point-in-time snapshot of the recorded effects.</summary>
    public IReadOnlyList<ActionExecutionRequest> Executions => executions.ToArray();

    /// <inheritdoc />
    public Task ExecuteAsync(ActionExecutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        executions.Enqueue(request);
        return Task.CompletedTask;
    }
}
