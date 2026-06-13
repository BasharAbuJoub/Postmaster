using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Postmaster.Core.Entities;

namespace Postmaster.EFCore.Manager
{
    internal class EfCoreOutboxManager<TContext> : IOutboxManager
        where TContext : DbContext
    {
        private readonly TContext _context;
        private DbSet<OutboxMessage> Messages => _context.Set<OutboxMessage>();

        public EfCoreOutboxManager(TContext context)
        {
            _context = context;
        }

        public async Task<OutboxMessageDetail?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var message = await Messages.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, ct);

            if (message == null)
            {
                return null;
            }

            return new OutboxMessageDetail
            {
                Id = message.Id,
                Url = message.Url,
                Method = message.Method,
                Headers = message.Headers,
                Payload = message.Payload,
                Channel = message.Channel,
                Status = message.Status,
                RetryCount = message.RetryCount,
                MaxRetryCount = message.MaxRetryCount,
                CreatedAt = message.CreatedAt,
                NextAttemptAt = message.NextAttemptAt,
                ProcessedAt = message.ProcessedAt,
                ResponseStatusCode = message.ResponseStatusCode,
                ElapsedMs = message.ElapsedMs,
                ResponseBody = message.ResponseBody,
                ErrorMessage = message.ErrorMessage,
                Metadata = message.Metadata,
                CorrelationId = message.CorrelationId,
            };
        }

        public async Task<OutboxPage> GetAsync(OutboxQuery query, CancellationToken ct = default)
        {
            var q = Messages.AsNoTracking();

            if (query.Status.HasValue)
                q = q.Where(x => x.Status == query.Status.Value);

            if (!string.IsNullOrEmpty(query.Channel))
                q = q.Where(x => x.Channel == query.Channel);

            if (query.From.HasValue)
                q = q.Where(x => x.CreatedAt >= query.From.Value);

            if (query.To.HasValue)
                q = q.Where(x => x.CreatedAt <= query.To.Value);

            if (!string.IsNullOrEmpty(query.CorrelationId))
                q = q.Where(x => x.CorrelationId == query.CorrelationId);

            if (!string.IsNullOrEmpty(query.MetadataContains))
                q = q.Where(x => x.Metadata != null && x.Metadata.Contains(query.MetadataContains));

            q = (query.SortBy, query.Ascending) switch
            {
                (OutboxSortBy.CreatedAt, true) => q.OrderBy(x => x.CreatedAt),
                (OutboxSortBy.CreatedAt, false) => q.OrderByDescending(x => x.CreatedAt),
                (OutboxSortBy.Status, true) => q.OrderBy(x => x.Status),
                (OutboxSortBy.Status, false) => q.OrderByDescending(x => x.Status),
                (OutboxSortBy.ElapsedMs, true) => q.OrderBy(x => x.ElapsedMs),
                (OutboxSortBy.ElapsedMs, false) => q.OrderByDescending(x => x.ElapsedMs),
                _ => q.OrderByDescending(x => x.CreatedAt),
            };

            var totalCount = await q.CountAsync(ct);

            var items = await q
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(x => new OutboxMessageSummary
                {
                    Id = x.Id,
                    Url = x.Url,
                    Method = x.Method,
                    Channel = x.Channel,
                    Status = x.Status,
                    RetryCount = x.RetryCount,
                    MaxRetryCount = x.MaxRetryCount,
                    CreatedAt = x.CreatedAt,
                    NextAttemptAt = x.NextAttemptAt,
                    ElapsedMs = x.ElapsedMs,
                    ErrorMessage = x.ErrorMessage,
                    ProcessedAt = x.ProcessedAt,
                    ResponseStatusCode = x.ResponseStatusCode,
                    CorrelationId = x.CorrelationId,
                })
                .ToListAsync(ct);

            return new OutboxPage
            {
                Items = items,
                TotalCount = totalCount,
                Page = query.Page,
                PageSize = query.PageSize,
            };
        }

        public async Task<OutboxStats> GetStatsAsync(CancellationToken ct = default)
        {
            var grouped = await Messages
                .AsNoTracking()
                .GroupBy(x => x.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            int CountFor(OutboxMessageStatus s) =>
                grouped.FirstOrDefault(x => x.Status == s)?.Count ?? 0;

            var succeeded = CountFor(OutboxMessageStatus.Succeeded);
            var failed = CountFor(OutboxMessageStatus.Failed);
            var dead = CountFor(OutboxMessageStatus.Dead);

            // SuccessRate is calculated against terminal attempted messages only —
            // Pending, Processing, and Cancelled are excluded since they were never fully attempted.
            var attempted = succeeded + failed + dead;

            var avgElapsed = await Messages
                .AsNoTracking()
                .Where(x => x.ElapsedMs.HasValue)
                .AverageAsync(x => (double?)x.ElapsedMs, ct) ?? 0;

            return new OutboxStats
            {
                Pending = CountFor(OutboxMessageStatus.Pending),
                Processing = CountFor(OutboxMessageStatus.Processing),
                Succeeded = succeeded,
                Failed = failed,
                Dead = dead,
                Cancelled = CountFor(OutboxMessageStatus.Cancelled),
                Total = grouped.Sum(x => x.Count),
                SuccessRate = attempted == 0 ? 0 : (double)succeeded / attempted * 100,
                AverageElapsedMs = avgElapsed,
            };
        }

        public async Task CancelAsync(Guid id, CancellationToken ct = default)
        {
            await Messages
                .Where(x => x.Id == id
                    && (x.Status == OutboxMessageStatus.Pending || x.Status == OutboxMessageStatus.Failed))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, OutboxMessageStatus.Cancelled), ct);
        }

        public async Task ResetAsync(Guid id, CancellationToken ct = default)
        {
            await Messages
                .Where(x => x.Id == id && x.Status != OutboxMessageStatus.Processing)
                .ExecuteUpdateAsync(ApplyResetSetters, ct);
        }

        public async Task ResetChannelAsync(string channel, CancellationToken ct = default)
        {
            await Messages
                .Where(x => x.Channel == channel
                    && (x.Status == OutboxMessageStatus.Failed || x.Status == OutboxMessageStatus.Dead))
                .ExecuteUpdateAsync(ApplyResetSetters, ct);
        }

        private static void ApplyResetSetters(UpdateSettersBuilder<OutboxMessage> s)
        {
            s.SetProperty(x => x.Status, OutboxMessageStatus.Pending)
                .SetProperty(x => x.RetryCount, 0)
                .SetProperty(x => x.NextAttemptAt, DateTime.UtcNow)
                .SetProperty(x => x.ErrorMessage, (string?)null)
                .SetProperty(x => x.ProcessedAt, (DateTime?)null)
                .SetProperty(x => x.ResponseStatusCode, (int?)null)
                .SetProperty(x => x.ElapsedMs, (long?)null)
                .SetProperty(x => x.ResponseBody, (string?)null);
        }
    }
}
