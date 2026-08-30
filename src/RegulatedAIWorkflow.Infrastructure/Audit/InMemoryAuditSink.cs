using System.Collections.Concurrent;
using RegulatedAIWorkflow.Core.Contracts.Audit;
using RegulatedAIWorkflow.Core.Ports;

namespace RegulatedAIWorkflow.Infrastructure.Audit;

/// <summary>Append-only in-memory audit storage. Stands in for durable WORM storage.</summary>
public sealed class InMemoryAuditSink : IAuditSink
{
    private readonly ConcurrentQueue<AuditEvent> events = new();

    /// <summary>A point-in-time snapshot of the recorded events.</summary>
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
