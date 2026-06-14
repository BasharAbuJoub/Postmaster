namespace Postmaster
{
    /// <summary>
    /// The result returned after successfully enqueuing a message.
    /// </summary>
    public sealed record OutboxMessageResult
    {
        /// <summary>The unique ID of the enqueued message.</summary>
        public Guid Id { get; init; }

        /// <summary>The UTC timestamp at which the message was enqueued.</summary>
        public DateTime CreatedAt { get; init; }
    }
}
