using Postmaster.Core.Entities;

namespace Postmaster
{
    /// <summary>
    /// A lightweight summary of an outbox message returned in paginated query results.
    /// For the full message including payload and response body, use <see cref="OutboxMessageDetail"/>.
    /// </summary>
    public class OutboxMessageSummary
    {
        /// <summary>The unique message ID.</summary>
        public Guid Id { get; init; }

        /// <summary>The target URL.</summary>
        public required string Url { get; init; }

        /// <summary>The HTTP method.</summary>
        public required string Method { get; init; }

        /// <summary>The channel this message belongs to, or <c>null</c> if channelless.</summary>
        public string? Channel { get; init; }

        /// <summary>The current processing status.</summary>
        public OutboxMessageStatus Status { get; init; }

        /// <summary>How many delivery attempts have been made so far.</summary>
        public int RetryCount { get; init; }

        /// <summary>The maximum number of delivery attempts before the message is marked Dead.</summary>
        public int MaxRetryCount { get; init; }

        /// <summary>The HTTP status code returned by the last delivery attempt, if any.</summary>
        public int? ResponseStatusCode { get; init; }

        /// <summary>The error message from the last failed delivery attempt, if any.</summary>
        public string? ErrorMessage { get; init; }

        /// <summary>How long the last delivery attempt took in milliseconds, if any.</summary>
        public long? ElapsedMs { get; init; }

        /// <summary>The UTC timestamp at which the message was enqueued.</summary>
        public DateTime CreatedAt { get; init; }

        /// <summary>The UTC timestamp at which the next delivery attempt is scheduled.</summary>
        public DateTime NextAttemptAt { get; init; }

        /// <summary>The UTC timestamp of the last delivery attempt, if any.</summary>
        public DateTime? ProcessedAt { get; init; }

        /// <summary>The correlation ID forwarded as <c>X-Correlation-Id</c> on the outgoing request.</summary>
        public required string CorrelationId { get; init; }
    }
}
