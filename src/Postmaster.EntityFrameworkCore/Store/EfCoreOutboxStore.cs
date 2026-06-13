using Microsoft.EntityFrameworkCore;
using Postmaster.Core.Abstractions;
using Postmaster.Core.Entities;

namespace Postmaster;

internal class EfCoreOutboxStore<TContext> : IOutboxStore
    where TContext : DbContext
{
    private readonly TContext _dbContext;
    private DbSet<OutboxMessage> Messages => _dbContext.Set<OutboxMessage>();

    public EfCoreOutboxStore(TContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> HasPendingAsync(CancellationToken ct)
    {
        return await Messages
            .AnyAsync(x => (x.Status == OutboxMessageStatus.Pending || x.Status == OutboxMessageStatus.Failed)
                && x.NextAttemptAt <= DateTime.UtcNow, ct);
    }

    public async Task SaveAsync(OutboxMessage message, CancellationToken ct)
    {
        await Messages.AddAsync(message, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task SaveBulkAsync(List<OutboxMessage> messages, CancellationToken ct)
    {
        await Messages.AddRangeAsync(messages, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<List<OutboxMessage>> AcquireAsync(int batchSize, string workerId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var blockedChannels = await Messages
            .Where(x => x.Channel != null
                && (x.Status == OutboxMessageStatus.Failed && x.NextAttemptAt > now
                    || x.Status == OutboxMessageStatus.Dead))
            .Select(x => x.Channel)
            .Distinct()
            .ToListAsync(ct);

        // Oldest candidate per named channel — small projection to avoid loading full entities
        var channelCandidates = await Messages
            .Where(x => x.Channel != null
                && (x.Status == OutboxMessageStatus.Pending || x.Status == OutboxMessageStatus.Failed)
                && x.NextAttemptAt <= now
                && x.LockedBy == null
                && !blockedChannels.Contains(x.Channel))
            .OrderBy(x => x.CreatedAt)
            .Select(x => new { x.Id, x.Channel, x.CreatedAt })
            .ToListAsync(ct);

        var channelEntries = channelCandidates
            .GroupBy(x => x.Channel)
            .Select(g => new { g.First().Id, g.First().CreatedAt })
            .ToList();

        // Null-channel candidates — bounded by batchSize since they are all independent
        var nullChannelEntries = await Messages
            .Where(x => x.Channel == null
                && (x.Status == OutboxMessageStatus.Pending || x.Status == OutboxMessageStatus.Failed)
                && x.NextAttemptAt <= now
                && x.LockedBy == null)
            .OrderBy(x => x.CreatedAt)
            .Take(batchSize)
            .Select(x => new { x.Id, x.CreatedAt })
            .ToListAsync(ct);

        // Merge both pools by age — oldest wins regardless of channel, neither pool can starve the other
        var ids = channelEntries
            .Concat(nullChannelEntries)
            .OrderBy(x => x.CreatedAt)
            .Take(batchSize)
            .Select(x => x.Id)
            .ToList();

        if (ids.Count == 0)
            return [];

        // Atomically claim rows — the WHERE LockedBy IS NULL guard ensures only one worker wins per row
        await Messages
            .Where(x => ids.Contains(x.Id) && x.LockedBy == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, OutboxMessageStatus.Processing)
                .SetProperty(x => x.LockedBy, workerId)
                .SetProperty(x => x.LockedAt, now), ct);

        return await Messages
            .Where(x => x.LockedBy == workerId)
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(OutboxMessage message, CancellationToken ct)
    {
        var workerId = message.LockedBy;

        // WHERE LockedBy = workerId ensures we only write if we still own the lock.
        // If recovery reset the row and another worker claimed it, this is a no-op.
        await Messages
            .Where(x => x.Id == message.Id && x.LockedBy == workerId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, message.Status)
                .SetProperty(x => x.RetryCount, message.RetryCount)
                .SetProperty(x => x.NextAttemptAt, message.NextAttemptAt)
                .SetProperty(x => x.ProcessedAt, message.ProcessedAt)
                .SetProperty(x => x.ResponseStatusCode, message.ResponseStatusCode)
                .SetProperty(x => x.ResponseBody, message.ResponseBody)
                .SetProperty(x => x.ElapsedMs, message.ElapsedMs)
                .SetProperty(x => x.ErrorMessage, message.ErrorMessage)
                .SetProperty(x => x.LockedBy, (string?)null)
                .SetProperty(x => x.LockedAt, (DateTime?)null), ct);
    }

    public async Task UpdateBulkAsync(List<OutboxMessage> messages, CancellationToken ct)
    {
        foreach (var message in messages)
            await UpdateAsync(message, ct);
    }

    public async Task RecoverStuckMessagesAsync(TimeSpan timeout, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow - timeout;

        await Messages
            .Where(x => x.Status == OutboxMessageStatus.Processing && x.LockedAt < cutoff)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, OutboxMessageStatus.Pending)
                .SetProperty(x => x.LockedBy, (string?)null)
                .SetProperty(x => x.LockedAt, (DateTime?)null)
                .SetProperty(x => x.NextAttemptAt, DateTime.UtcNow), ct);
    }
}
