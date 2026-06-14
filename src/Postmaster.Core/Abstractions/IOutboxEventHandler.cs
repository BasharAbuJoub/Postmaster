namespace Postmaster.Core.Abstractions
{
    /// <summary>
    /// Implement and register this interface to observe outbox dispatch outcomes.
    /// All registered handlers are called after each message is dispatched.
    /// Handlers are resolved from the DI scope of the message being processed,
    /// so scoped dependencies are supported.
    /// </summary>
    public interface IOutboxEventHandler
    {
        /// <summary>Called after a message has been dispatched and its result persisted.</summary>
        Task OnDispatchedAsync(OutboxDispatchResult result, CancellationToken ct = default);
    }
}
