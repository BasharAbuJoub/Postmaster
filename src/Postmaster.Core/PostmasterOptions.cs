namespace Postmaster.Core
{
    /// <summary>
    /// Configuration options for the Postmaster outbox processor.
    /// </summary>
    public class PostmasterOptions
    {
        /// <summary>
        /// Number of messages acquired and processed per cycle.
        /// Default: <c>10</c>.
        /// </summary>
        public int BatchSize { get; set; } = 10;

        /// <summary>
        /// Default retry limit applied to messages that do not specify their own
        /// <see cref="Models.OutboxRequest.MaxRetryCount"/>.
        /// Default: <c>3</c>.
        /// </summary>
        public int DefaultMaxRetryCount { get; set; } = 3;

        /// <summary>
        /// How long the processor waits before polling again when the outbox is empty.
        /// Default: <c>30 seconds</c>.
        /// </summary>
        public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// How long a message can remain in <see cref="Entities.OutboxMessageStatus.Processing"/>
        /// before the recovery sweep resets it back to <see cref="Entities.OutboxMessageStatus.Pending"/>.
        /// Should be comfortably longer than the longest expected HTTP request.
        /// Default: <c>10 minutes</c>.
        /// </summary>
        public TimeSpan ProcessingTimeout { get; set; } = TimeSpan.FromMinutes(10);
    }
}
