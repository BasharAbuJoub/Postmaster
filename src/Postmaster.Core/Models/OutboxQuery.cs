namespace Postmaster
{
    /// <summary>
    /// Filters and pagination options for <see cref="IOutboxManager.GetAsync"/>.
    /// All filters are optional and combined with AND logic.
    /// </summary>
    public sealed class OutboxQuery
    {
        /// <summary>Filter to messages whose <c>CorrelationId</c> exactly matches this value.</summary>
        public string? CorrelationId { get; init; }

        /// <summary>Filter to messages whose <c>Metadata</c> contains this substring.</summary>
        public string? MetadataContains { get; init; }

        /// <summary>Filter to messages with this status.</summary>
        public OutboxMessageStatus? Status { get; init; }

        /// <summary>Filter to messages belonging to this channel.</summary>
        public string? Channel { get; init; }

        /// <summary>Filter to messages created on or after this UTC timestamp.</summary>
        public DateTime? From { get; init; }

        /// <summary>Filter to messages created on or before this UTC timestamp.</summary>
        public DateTime? To { get; init; }

        /// <summary>The field to sort by. Default: <see cref="OutboxSortBy.CreatedAt"/>.</summary>
        public OutboxSortBy SortBy { get; init; } = OutboxSortBy.CreatedAt;

        /// <summary>Sort ascending when <c>true</c>, descending when <c>false</c>. Default: <c>false</c>.</summary>
        public bool Ascending { get; init; } = false;

        /// <summary>1-based page number. Default: <c>1</c>.</summary>
        public int Page { get; init; } = 1;

        /// <summary>Number of items per page. Default: <c>20</c>.</summary>
        public int PageSize { get; init; } = 20;
    }
}
