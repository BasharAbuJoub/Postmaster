using Postmaster.Core.Entities;

namespace Postmaster.Core.Abstractions
{
    /// <summary>
    /// Persistence contract for outbox messages. Implement this interface to provide
    /// a custom storage backend (e.g. Redis, Dapper, MongoDB).
    /// </summary>
    public interface IOutboxStore
    {
        /// <summary>
        /// Atomically claims up to <paramref name="batchSize"/> pending messages for the given worker,
        /// marking them as <see cref="OutboxMessageStatus.Processing"/>.
        /// Messages in a blocked channel (dead or not-yet-retryable failed message) are excluded.
        /// Named-channel messages are limited to one per channel per batch.
        /// </summary>
        Task<List<OutboxMessage>> AcquireAsync(int batchSize, string workerId, CancellationToken ct);

        /// <summary>
        /// Returns <c>true</c> if there are any messages ready to be processed right now.
        /// </summary>
        Task<bool> HasPendingAsync(CancellationToken ct);

        /// <summary>
        /// Persists a new outbox message.
        /// </summary>
        Task SaveAsync(OutboxMessage message, CancellationToken ct);

        /// <summary>
        /// Persists multiple new outbox messages in a single operation.
        /// </summary>
        Task SaveBulkAsync(List<OutboxMessage> messages, CancellationToken ct);

        /// <summary>
        /// Updates an existing message after processing. Clears the worker lock.
        /// If the worker no longer owns the lock (recovery reset it), this is a no-op.
        /// </summary>
        Task UpdateAsync(OutboxMessage message, CancellationToken ct);

        /// <summary>
        /// Updates multiple existing messages after processing. Clears the worker lock on each.
        /// </summary>
        Task UpdateBulkAsync(List<OutboxMessage> messages, CancellationToken ct);

        /// <summary>
        /// Resets any <see cref="OutboxMessageStatus.Processing"/> messages whose lock
        /// is older than <paramref name="timeout"/> back to <see cref="OutboxMessageStatus.Pending"/>.
        /// Protects against messages stuck in Processing due to a crashed worker.
        /// </summary>
        Task RecoverStuckMessagesAsync(TimeSpan timeout, CancellationToken ct);
    }
}
