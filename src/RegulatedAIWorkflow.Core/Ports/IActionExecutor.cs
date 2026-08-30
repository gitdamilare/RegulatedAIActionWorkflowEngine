using RegulatedAIWorkflow.Core.Domain.Execution;

namespace RegulatedAIWorkflow.Core.Ports;

/// <summary>
/// Performs the regulated side effect. Reached only after every gate and the pre-effect audit write.
/// A failure must throw: returning normally asserts the effect happened, and an adapter must not
/// convert a timeout into either answer.
/// </summary>
public interface IActionExecutor
{
    /// <summary>Executes the requested action, or throws if it could not be completed.</summary>
    Task ExecuteAsync(ActionExecutionRequest request, CancellationToken cancellationToken);
}
