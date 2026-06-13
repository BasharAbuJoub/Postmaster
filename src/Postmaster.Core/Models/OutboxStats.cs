namespace Postmaster
{
    /// <summary>
    /// Aggregate statistics across all outbox messages.
    /// </summary>
    public sealed class OutboxStats
    {
        /// <summary>Number of messages waiting to be processed.</summary>
        public int Pending { get; init; }

        /// <summary>Number of messages currently being dispatched.</summary>
        public int Processing { get; init; }

        /// <summary>Number of messages successfully delivered.</summary>
        public int Succeeded { get; init; }

        /// <summary>Number of messages that failed and are scheduled for retry.</summary>
        public int Failed { get; init; }

        /// <summary>Number of messages that exhausted all retries.</summary>
        public int Dead { get; init; }

        /// <summary>Number of messages that were manually cancelled.</summary>
        public int Cancelled { get; init; }

        /// <summary>Total number of messages across all statuses.</summary>
        public int Total { get; init; }

        /// <summary>Average elapsed milliseconds across all attempted messages.</summary>
        public double AverageElapsedMs { get; init; }

        /// <summary>
        /// Percentage of messages that succeeded out of all attempted messages
        /// (<see cref="Succeeded"/> + <see cref="Failed"/> + <see cref="Dead"/>).
        /// Pending, Processing, and Cancelled messages are excluded from this calculation.
        /// </summary>
        public double SuccessRate { get; init; }
    }
}
