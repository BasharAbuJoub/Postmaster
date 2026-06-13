using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Postmaster.Core.Abstractions;
using Postmaster.Core.Entities;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Postmaster.Core.Processor
{
    internal class OutboxProcessor : IOutboxProcessor
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly PostmasterOptions _options;
        private readonly ILogger<OutboxProcessor> _logger;
        private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}";

        public OutboxProcessor(IServiceScopeFactory serviceScopeFactory,
            IHttpClientFactory httpClientFactory,
            IOptions<PostmasterOptions> options,
            ILogger<OutboxProcessor> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<bool> ProcessAsync(CancellationToken ct = default)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

            await store.RecoverStuckMessagesAsync(_options.ProcessingTimeout, ct);

            var messages = await store.AcquireAsync(_options.BatchSize, _workerId, ct);

            if (messages.Count == 0)
                return false;

            await Task.WhenAll(messages.Select(message => ProcessMessageAsync(message, ct)));

            return await store.HasPendingAsync(ct);
        }

        private async Task ProcessMessageAsync(OutboxMessage message, CancellationToken ct)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var store    = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
            var handlers = scope.ServiceProvider.GetServices<IOutboxEventHandler>();

            try
            {
                var client = _httpClientFactory.CreateClient("Postmaster");
                var request = BuildRequest(message);
                var stopwatch = Stopwatch.StartNew();

                var response = await client.SendAsync(request, ct);

                stopwatch.Stop();

                message.ResponseStatusCode = (int)response.StatusCode;
                message.ResponseBody = await response.Content.ReadAsStringAsync(ct);
                message.ProcessedAt = DateTime.UtcNow;
                message.ElapsedMs = stopwatch.ElapsedMilliseconds;

                if (response.IsSuccessStatusCode)
                {
                    message.Status = OutboxMessageStatus.Succeeded;
                    message.ErrorMessage = null;
                    _logger.LogInformation("Postmaster: message {Id} succeeded", message.Id);
                }
                else
                {
                    _logger.LogWarning("Postmaster: message {Id} failed with HTTP {StatusCode}", message.Id, (int)response.StatusCode);
                    HandleFailure(message, $"HTTP {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                message.ProcessedAt = DateTime.UtcNow;
                HandleFailure(message, ex.Message);
                _logger.LogError(ex, "Postmaster: message {Id} failed", message.Id);
            }

            await store.UpdateAsync(message, CancellationToken.None);

            var result = new OutboxDispatchResult
            {
                Id                 = message.Id,
                CorrelationId      = message.CorrelationId,
                Channel            = message.Channel,
                Metadata           = message.Metadata,
                Succeeded          = message.Status == OutboxMessageStatus.Succeeded,
                ResponseStatusCode = message.ResponseStatusCode,
                ElapsedMs          = message.ElapsedMs,
                ErrorMessage       = message.ErrorMessage,
            };

            foreach (var handler in handlers)
            {
                try { await handler.OnDispatchedAsync(result, ct); }
                catch (Exception ex) { _logger.LogError(ex, "Postmaster: event handler {Handler} threw an exception", handler.GetType().Name); }
            }
        }

        private void HandleFailure(OutboxMessage message, string error)
        {
            message.ErrorMessage = error;
            message.RetryCount++;

            if (message.RetryCount >= message.MaxRetryCount)
            {
                message.Status = OutboxMessageStatus.Dead;
                _logger.LogError("Postmaster: message {Id} is dead after {Retries} retries", message.Id, message.RetryCount);
            }
            else
            {
                message.Status = OutboxMessageStatus.Failed;
                message.NextAttemptAt = DateTime.UtcNow + TimeSpan.FromMinutes(Math.Pow(2, message.RetryCount));
            }
        }

        private HttpRequestMessage BuildRequest(OutboxMessage message)
        {
            var request = new HttpRequestMessage(
                new HttpMethod(message.Method),
                message.Url);

            Dictionary<string, string>? headers = null;

            if (!string.IsNullOrEmpty(message.Headers))
                headers = JsonSerializer.Deserialize<Dictionary<string, string>>(message.Headers);

            if (!string.IsNullOrEmpty(message.Payload))
            {
                var contentType = headers?.GetValueOrDefault("Content-Type") ?? "application/json";
                request.Content = new StringContent(message.Payload, Encoding.UTF8, contentType);
            }

            if (headers != null)
                foreach (var (key, value) in headers)
                    if (!key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                        request.Headers.TryAddWithoutValidation(key, value);

            if (!string.IsNullOrEmpty(message.CorrelationId))
                request.Headers.TryAddWithoutValidation("X-Correlation-Id", message.CorrelationId);

            return request;
        }
    }
}
