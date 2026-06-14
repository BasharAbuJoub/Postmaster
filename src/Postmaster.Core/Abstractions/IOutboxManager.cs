namespace Postmaster
{
    /// <summary>
    /// Provides query and management operations over outbox messages.
    /// </summary>
    public interface IOutboxManager
    {
        /// <summary>
        /// Returns the full details of a single message, or <c>null</c> if not found.
        /// </summary>
        Task<OutboxMessageDetail?> GetByIdAsync(Guid id, CancellationToken ct = default);

        /// <summary>
        /// Returns a paginated, filtered list of message summaries.
        /// </summary>
        Task<OutboxPage> GetAsync(OutboxQuery query, CancellationToken ct = default);

        /// <summary>
        /// Returns aggregate statistics across all messages.
        /// </summary>
        Task<OutboxStats> GetStatsAsync(CancellationToken ct = default);

        /// <summary>
        /// Resets a message back to <see cref="OutboxMessageStatus.Pending"/> for reprocessing.
        /// Has no effect if the message is currently <see cref="OutboxMessageStatus.Processing"/>.
        /// </summary>
        Task ResetAsync(Guid id, CancellationToken ct = default);

        /// <summary>
        /// Resets all <see cref="OutboxMessageStatus.Failed"/> and <see cref="OutboxMessageStatus.Dead"/>
        /// messages in a channel back to <see cref="OutboxMessageStatus.Pending"/>.
        /// </summary>
        Task ResetChannelAsync(string channel, CancellationToken ct = default);

        /// <summary>
        /// Cancels a <see cref="OutboxMessageStatus.Pending"/> or <see cref="OutboxMessageStatus.Failed"/>
        /// message. Has no effect on messages in any other state.
        /// </summary>
        Task CancelAsync(Guid id, CancellationToken ct = default);
    }
}
