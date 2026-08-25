using RegulatedAIWorkflow.Core.Domain.Execution;

namespace RegulatedAIWorkflow.Core.Ports;

/// <summary>
/// Executes a validated regulated action after every applicable gate passes.
/// </summary>
/// <remarks>
/// A result with <see cref="ActionExecutionResult.Succeeded"/> set to <see langword="false"/>
/// asserts that no regulated effect occurred. Once this method is invoked, an exception or
/// cancellation leaves the effect outcome unknown; adapters must not translate a timeout or
/// otherwise uncertain downstream outcome into a result with <c>Succeeded: false</c>.
/// </remarks>
public interface IActionExecutor
{
    Task<ActionExecutionResult> ExecuteAsync(
        ActionExecutionRequest request,
        CancellationToken cancellationToken);
}
