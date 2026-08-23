using System.Collections.Concurrent;
using RegulatedAIWorkflow.Core.Domain.Execution;
using RegulatedAIWorkflow.Core.Ports;

namespace RegulatedAIWorkflow.Infrastructure.Execution;

/// <summary>
/// Records deterministic mock action effects in memory.
/// </summary>
public sealed class InMemoryActionExecutor : IActionExecutor
{
    private readonly ConcurrentQueue<ActionExecutionRequest> executions = new();

    /// <summary>Gets a point-in-time snapshot of recorded effects.</summary>
    public IReadOnlyList<ActionExecutionRequest> Executions => executions.ToArray();

    /// <inheritdoc />
    public Task<ActionExecutionResult> ExecuteAsync(
        ActionExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        executions.Enqueue(request);
        return Task.FromResult(new ActionExecutionResult(Succeeded: true));
    }
}
