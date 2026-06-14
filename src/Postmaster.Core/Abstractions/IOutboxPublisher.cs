namespace Postmaster
{
    /// <summary>
    /// Enqueues HTTP requests into the outbox for reliable background delivery.
    /// </summary>
    public interface IOutboxPublisher
    {
        /// <summary>
        /// Enqueues a single HTTP request for delivery.
        /// </summary>
        /// <param name="request">The request to enqueue.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The result containing the message ID and creation timestamp.</returns>
        Task<OutboxMessageResult> EnqueueAsync(OutboxRequest request, CancellationToken ct = default);

        /// <summary>
        /// Enqueues multiple HTTP requests in a single batch.
        /// </summary>
        /// <param name="requests">The requests to enqueue.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A list of results, one per enqueued request, in the same order.</returns>
        Task<List<OutboxMessageResult>> EnqueueBulkAsync(IEnumerable<OutboxRequest> requests, CancellationToken ct = default);
    }
}
