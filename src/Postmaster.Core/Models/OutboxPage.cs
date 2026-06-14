namespace Postmaster
{
    /// <summary>
    /// A paginated result set of outbox message summaries.
    /// </summary>
    public sealed class OutboxPage
    {
        /// <summary>The messages on the current page.</summary>
        public List<OutboxMessageSummary> Items { get; init; } = [];

        /// <summary>The current 1-based page number.</summary>
        public int Page { get; init; }

        /// <summary>The number of items per page.</summary>
        public int PageSize { get; init; }

        /// <summary>The total number of messages matching the query across all pages.</summary>
        public int TotalCount { get; init; }

        /// <summary>The total number of pages.</summary>
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

        /// <summary><c>true</c> if there is a page before this one.</summary>
        public bool HasPreviousPage => Page > 1;

        /// <summary><c>true</c> if there is a page after this one.</summary>
        public bool HasNextPage => Page < TotalPages;
    }
}
