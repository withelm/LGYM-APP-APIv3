using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

/// <summary>
/// Persisted API-layer idempotency record for public mutating endpoints with side effects.
/// Enables replay-safe duplicate request handling and conflict detection.
/// </summary>
public sealed class ApiIdempotencyRecord : EntityBase<ApiIdempotencyRecord>
{
    /// <summary>
    /// Client-provided idempotency key (header value).
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>
    /// Scope tuple for partitioning keys: (HTTP method, route template, caller scope).
    /// Example: "POST|/api/register|user@example.com"
    /// </summary>
    public string ScopeTuple { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 fingerprint of canonical request (body + route params + query + scope).
    /// Used to detect same-key-different-body conflicts.
    /// </summary>
    public string RequestFingerprint { get; set; } = string.Empty;

    /// <summary>
    /// HTTP status code from the original response.
    /// </summary>
    public int ResponseStatusCode { get; set; }

    /// <summary>
    /// Serialized JSON response body from the original request.
    /// Replayed for identical subsequent requests.
    /// </summary>
    public string ResponseBodyJson { get; set; } = string.Empty;

    /// <summary>
    /// Optional reference to the primary CommandEnvelope created for this request.
    /// </summary>
    public Id<CommandEnvelope>? CommandEnvelopeId { get; set; }

    /// <summary>
    /// Optional reference to the primary NotificationMessage created for this request.
    /// </summary>
    public Id<NotificationMessage>? NotificationMessageId { get; set; }

    /// <summary>
    /// Timestamp when the original request was processed.
    /// </summary>
    public DateTimeOffset ProcessedAt { get; set; }
}
