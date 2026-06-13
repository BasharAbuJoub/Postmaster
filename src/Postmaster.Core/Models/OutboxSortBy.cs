namespace Postmaster
{
    /// <summary>
    /// Sort fields available for <see cref="OutboxQuery.SortBy"/>.
    /// </summary>
    public enum OutboxSortBy
    {
        /// <summary>Sort by the time the message was enqueued.</summary>
        CreatedAt,

        /// <summary>Sort by the current message status.</summary>
        Status,

        /// <summary>Sort by the duration of the last delivery attempt in milliseconds.</summary>
        ElapsedMs,
    }
}
