using Hangfire.Console;
using Hangfire.Server;
using Microsoft.Extensions.Logging;
using Postmaster.Core.Abstractions;

namespace Postmaster.Hangfire
{
    /// <summary>
    /// Hangfire job that drains the outbox in a single invocation.
    /// Loops until <see cref="IOutboxProcessor.ProcessAsync"/> reports no more pending messages,
    /// then returns so Hangfire can schedule the next run based on the configured interval.
    /// </summary>
    public class OutboxHangfireJob
    {
        private readonly IOutboxProcessor _processor;
        private readonly ILogger<OutboxHangfireJob> _logger;

        /// <summary>Initializes the job with the outbox processor.</summary>
        public OutboxHangfireJob(IOutboxProcessor processor, ILogger<OutboxHangfireJob> logger)
        {
            _processor = processor;
            _logger = logger;
        }

        /// <summary>Processes all currently pending messages.</summary>
        public async Task ExecuteAsync(PerformContext context, CancellationToken cancellationToken = default)
        {
            PerformContextAmbient.Current = context;
            _logger.LogInformation("Starting Postmaster outbox job");

            try
            {
                while (await _processor.ProcessAsync(cancellationToken)) { }
                _logger.LogInformation("Postmaster outbox job completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Postmaster outbox job failed");
                throw;
            }
        }
    }
}
