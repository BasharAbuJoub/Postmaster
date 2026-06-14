using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Postmaster.Core.Abstractions;

namespace Postmaster.Core.Processor
{
    internal class OutboxProcessorHost : BackgroundService
    {
        private readonly IOutboxProcessor _processor;
        private readonly PostmasterOptions _options;
        private readonly ILogger<OutboxProcessorHost> _logger;

        public OutboxProcessorHost(IOutboxProcessor processor,
            IOptions<PostmasterOptions> options,
            ILogger<OutboxProcessorHost> logger)
        {
            _processor = processor;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Postmaster processor started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var hasMoreRequest = await _processor.ProcessAsync(stoppingToken);

                    if (!hasMoreRequest)
                    {
                        _logger.LogDebug("Postmaster processor is idle, waiting for {PollingInterval} before next check",
                            _options.PollingInterval);
                        await Task.Delay(_options.PollingInterval, stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Postmaster processor encountered an error");

                    await Task.Delay(_options.PollingInterval, stoppingToken);
                }
            }

            _logger.LogInformation("Postmaster processor stopped");
        }
    }
}
