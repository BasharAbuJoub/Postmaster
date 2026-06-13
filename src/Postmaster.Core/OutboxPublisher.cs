using Microsoft.Extensions.Options;
using Postmaster.Core.Abstractions;
using Postmaster.Core.Entities;
using System.Text.Json;

namespace Postmaster.Core
{
    internal class OutboxPublisher : IOutboxPublisher
    {
        private readonly IOutboxStore _store;
        private readonly PostmasterOptions _options;

        public OutboxPublisher(
            IOutboxStore store,
            IOptions<PostmasterOptions> options)
        {
            _store = store;
            _options = options.Value;
        }

        public async Task<OutboxMessageResult> EnqueueAsync(OutboxRequest request, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var message = Map(request);

            await _store.SaveAsync(message, ct);

            return new OutboxMessageResult { Id = message.Id, CreatedAt = message.CreatedAt };
        }

        public async Task<List<OutboxMessageResult>> EnqueueBulkAsync(IEnumerable<OutboxRequest> requests, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(requests);

            var messages = requests.Select(Map).ToList();

            if (messages.Count == 0)
            {
                return [];
            }

            await _store.SaveBulkAsync(messages, ct);

            return messages.Select(m => new OutboxMessageResult { Id = m.Id, CreatedAt = m.CreatedAt }).ToList();
        }

        private OutboxMessage Map(OutboxRequest request)
        {
            ArgumentException.ThrowIfNullOrEmpty(request.Url, nameof(request.Url));
            ArgumentException.ThrowIfNullOrEmpty(request.Method, nameof(request.Method));

            return new OutboxMessage
            {
                Url = request.Url,
                Method = request.Method.ToUpperInvariant(),
                Headers = JsonSerializer.Serialize(request.Headers ?? new Dictionary<string, string>()),
                Payload = request.Payload,
                Channel = request.Channel,
                Status = OutboxMessageStatus.Pending,
                RetryCount = 0,
                MaxRetryCount = request.MaxRetryCount ?? _options.DefaultMaxRetryCount,
                NextAttemptAt = request.ScheduleAt ?? DateTime.UtcNow,
                Metadata = request.Metadata,
                CorrelationId = request.CorrelationId ?? Guid.NewGuid().ToString(),
            };
        }
    }
}
