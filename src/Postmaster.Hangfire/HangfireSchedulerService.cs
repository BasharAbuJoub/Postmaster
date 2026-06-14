using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Postmaster.Core;

namespace Postmaster.Hangfire
{
    internal sealed class HangfireSchedulerService : IHostedService
    {
        private const string JobId = "postmaster-outbox";

        private readonly IRecurringJobManager _jobManager;
        private readonly PostmasterOptions _options;

        public HangfireSchedulerService(IRecurringJobManager jobManager, IOptions<PostmasterOptions> options)
        {
            _jobManager = jobManager;
            _options = options.Value;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _jobManager.AddOrUpdate<OutboxHangfireJob>(
                JobId,
                job => job.ExecuteAsync(null!, CancellationToken.None),
                ToCron(_options.PollingInterval));

            return Task.CompletedTask;
        }

        // Cron minimum granularity is 1 minute; sub-minute intervals are clamped up.
        private static string ToCron(TimeSpan interval)
        {
            var minutes = Math.Max(1, (int)interval.TotalMinutes);
            if (minutes < 60)  return Cron.MinuteInterval(minutes);
            var hours = Math.Max(1, (int)interval.TotalHours);
            if (hours < 24)    return Cron.HourInterval(hours);
            return Cron.Daily();
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
