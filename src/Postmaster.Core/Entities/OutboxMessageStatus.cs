namespace Postmaster
{
    /// <summary>
    /// The processing status of an outbox message.
    /// </summary>
    public enum OutboxMessageStatus
    {
        /// <summary>The message is waiting to be picked up by the processor.</summary>
        Pending = 0,

        /// <summary>The message has been claimed by a worker and is being dispatched.</summary>
        Processing = 1,

        /// <summary>The HTTP request completed with a 2xx response.</summary>
        Succeeded = 2,

        /// <summary>The HTTP request failed. The message is scheduled for retry.</summary>
        Failed = 3,

        /// <summary>The message exhausted all retry attempts. Manual intervention required.</summary>
        Dead = 4,

        /// <summary>The message was manually cancelled before it could be delivered.</summary>
        Cancelled = 5,
    }
}
