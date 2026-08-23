namespace RegulatedAIWorkflow.Core.Domain.Execution;

/// <summary>
/// Reports whether the mock action completed successfully.
/// </summary>
public sealed record ActionExecutionResult(bool Succeeded);
