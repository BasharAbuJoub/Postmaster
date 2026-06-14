namespace Postmaster.Core.Abstractions
{
    /// <summary>
    /// Acquires and dispatches a batch of pending outbox messages.
    /// Implement a custom processor host by calling <see cref="ProcessAsync"/>
    /// from your job instead of using the built-in <c>UseBackgroundService()</c>.
    /// </summary>
    public interface IOutboxProcessor
    {
        /// <summary>
        /// Processes one batch of pending messages.
        /// </summary>
        /// <returns>
        /// <c>true</c> if there are more pending messages after this batch;
        /// <c>false</c> if the outbox is empty.
        /// </returns>
        Task<bool> ProcessAsync(CancellationToken ct = default);
    }
}
