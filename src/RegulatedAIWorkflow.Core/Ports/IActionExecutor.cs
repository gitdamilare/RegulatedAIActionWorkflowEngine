using RegulatedAIWorkflow.Core.Domain.Execution;

namespace RegulatedAIWorkflow.Core.Ports;

/// <summary>
/// Executes a validated regulated action after every applicable gate passes.
/// </summary>
public interface IActionExecutor
{
    Task<ActionExecutionResult> ExecuteAsync(
        ActionExecutionRequest request,
        CancellationToken cancellationToken);
}
