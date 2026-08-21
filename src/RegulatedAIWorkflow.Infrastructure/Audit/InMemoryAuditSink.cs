using System.Collections.Concurrent;
using RegulatedAIWorkflow.Core.Contracts.Audit;
using RegulatedAIWorkflow.Core.Ports;

namespace RegulatedAIWorkflow.Infrastructure.Audit;

/// <summary>
/// Provides a thread-safe append-only audit adapter for the in-memory workflow slice.
/// </summary>
public sealed class InMemoryAuditSink : IAuditSink
{
    private readonly ConcurrentQueue<AuditEvent> events = new();

    /// <summary>
    /// Gets a point-in-time snapshot of the recorded events.
    /// </summary>
    public IReadOnlyList<AuditEvent> Events => events.ToArray();

    /// <inheritdoc />
    public Task WriteAuditEventAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        cancellationToken.ThrowIfCancellationRequested();

        events.Enqueue(auditEvent);
        return Task.CompletedTask;
    }
}
