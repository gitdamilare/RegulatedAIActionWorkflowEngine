using RegulatedAIWorkflow.Core.Contracts.Audit;

namespace RegulatedAIWorkflow.Core.Ports;

/// <summary>
/// Persists safe structured workflow audit events.
/// </summary>
public interface IAuditSink
{
    /// <summary>
    /// Writes an event to the audit sink.
    /// </summary>
    /// <param name="auditEvent">The safe structured event to append.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task WriteAuditEventAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken);
}
