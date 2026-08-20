namespace RegulatedAIWorkflow.Core.Contracts.Workflow;

/// <summary>
/// Provides a verified document citation for presentation to a caller.
/// </summary>
/// <param name="DocumentId">The cited evidence document identifier.</param>
/// <param name="Snippet">A safe snippet produced by a trusted citation resolver.</param>
public sealed record Citation(string DocumentId, string Snippet);
