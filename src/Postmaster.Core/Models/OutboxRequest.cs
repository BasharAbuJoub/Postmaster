namespace Postmaster
{
    /// <summary>
    /// Describes an HTTP request to be enqueued in the outbox.
    /// </summary>
    public sealed record OutboxRequest
    {
        /// <summary>
        /// The target URL. Required.
        /// </summary>
        public required string Url { get; init; }

        /// <summary>
        /// The HTTP method (e.g. <c>POST</c>, <c>PUT</c>). Required. Case-insensitive.
        /// </summary>
        public required string Method { get; init; }

        /// <summary>
        /// Optional HTTP headers to include in the request.
        /// Specify <c>Content-Type</c> here to override the default <c>application/json</c>.
        /// </summary>
        public IReadOnlyDictionary<string, string>? Headers { get; init; }

        /// <summary>
        /// Optional request body. Sent as-is.
        /// </summary>
        public string? Payload { get; init; }

        /// <summary>
        /// Optional channel name. Messages in the same channel are processed one at a time,
        /// in enqueue order. A channel is blocked until any dead message in it is resolved.
        /// Messages without a channel are processed concurrently.
        /// </summary>
        public string? Channel { get; init; }

        /// <summary>
        /// Maximum number of delivery attempts before the message is marked <see cref="OutboxMessageStatus.Dead"/>.
        /// Defaults to <see cref="PostmasterOptions.DefaultMaxRetryCount"/> when not specified.
        /// </summary>
        public int? MaxRetryCount { get; init; }

        /// <summary>
        /// Delays delivery until this UTC time. Defaults to immediate delivery when not specified.
        /// </summary>
        public DateTime? ScheduleAt { get; init; }

        /// <summary>
        /// Optional arbitrary string metadata stored with the message. Useful for filtering
        /// via <see cref="OutboxQuery.MetadataContains"/> or for debugging.
        /// </summary>
        public string? Metadata { get; init; }

        /// <summary>
        /// Optional correlation ID forwarded as <c>X-Correlation-Id</c> on the outgoing request.
        /// When not provided, a unique GUID is generated automatically.
        /// </summary>
        public string? CorrelationId { get; init; }
    }
}
