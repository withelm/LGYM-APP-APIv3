using System;

namespace LgymApi.BackgroundWorker.Common;

/// <summary>
/// Provides idempotency key calculation and comparison for correlation ID-based deduplication.
/// Idempotency keys are stable, deterministic values derived from correlation IDs
/// to enable durable duplicate command-action prevention at repository and dispatcher levels.
/// </summary>
public static class IdempotencyKeyPolicy
{
    /// <summary>
    /// Calculates an idempotency key from a correlation ID.
    /// The key is a stable, deterministic representation suitable for database storage and lookup.
    /// </summary>
    /// <param name="correlationId">Correlation ID (must not be Guid.Empty)</param>
    /// <returns>Idempotency key as string (format: correlation ID in "D" format)</returns>
    /// <exception cref="ArgumentException">Thrown if correlationId is Guid.Empty</exception>
    public static string CalculateKey(Guid correlationId)
    {
        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException("Correlation ID cannot be empty.", nameof(correlationId));
        }

        return correlationId.ToString("D");
    }

    /// <summary>
    /// Compares two idempotency keys for equality using ordinal string comparison.
    /// Handles null values correctly: null == null returns true, null != non-null returns false.
    /// </summary>
    /// <param name="key1">First idempotency key (may be null)</param>
    /// <param name="key2">Second idempotency key (may be null)</param>
    /// <returns>True if keys are equal (including both null), false otherwise</returns>
    public static bool AreKeysEqual(string? key1, string? key2)
    {
        return string.Equals(key1, key2, StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines whether an idempotency key matches the expected key for a given correlation ID.
    /// Used to verify that a persisted key corresponds to the provided correlation ID.
    /// </summary>
    /// <param name="idempotencyKey">Idempotency key to verify (may be null)</param>
    /// <param name="correlationId">Correlation ID to match against</param>
    /// <returns>True if the key matches the correlation ID, false otherwise</returns>
    public static bool IsKeyForCorrelation(string? idempotencyKey, Guid correlationId)
    {
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            return false;
        }

        var expectedKey = CalculateKey(correlationId);
        return AreKeysEqual(idempotencyKey, expectedKey);
    }
}
