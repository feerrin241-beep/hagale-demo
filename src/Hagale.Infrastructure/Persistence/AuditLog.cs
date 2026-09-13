namespace Hagale.Infrastructure.Persistence;

public sealed class AuditLog
{
    private AuditLog()
    {
    }

    public AuditLog(
        Guid? actorUserId,
        string requestMethod,
        string requestPath,
        int responseStatusCode,
        string traceIdentifier,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestMethod);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(traceIdentifier);

        Id = Guid.NewGuid();
        ActorUserId = actorUserId;
        RequestMethod = requestMethod;
        RequestPath = requestPath;
        ResponseStatusCode = responseStatusCode;
        TraceIdentifier = traceIdentifier;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string RequestMethod { get; private set; } = null!;
    public string RequestPath { get; private set; } = null!;
    public int ResponseStatusCode { get; private set; }
    public string TraceIdentifier { get; private set; } = null!;
    public DateTimeOffset OccurredAtUtc { get; private set; }
}
