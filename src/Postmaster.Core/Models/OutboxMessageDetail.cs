namespace Postmaster
{
    /// <summary>
    /// Full details of an outbox message including payload, headers, and response body.
    /// Returned by <see cref="IOutboxManager.GetByIdAsync"/>.
    /// </summary>
    public class OutboxMessageDetail : OutboxMessageSummary
    {
        /// <summary>The JSON-serialized request headers.</summary>
        public required string Headers { get; init; }

        /// <summary>The raw request payload, if any.</summary>
        public string? Payload { get; init; }

        /// <summary>The raw response body from the last delivery attempt, if any.</summary>
        public string? ResponseBody { get; init; }

        /// <summary>The metadata string attached at enqueue time, if any.</summary>
        public string? Metadata { get; init; }
    }
}
