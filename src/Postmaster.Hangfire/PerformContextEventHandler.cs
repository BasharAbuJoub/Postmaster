using Hangfire.Console;
using Postmaster.Core.Abstractions;

namespace Postmaster.Hangfire
{
    internal sealed class PerformContextEventHandler : IOutboxEventHandler
    {
        public Task OnDispatchedAsync(OutboxDispatchResult r, CancellationToken ct = default)
        {
            var context = PerformContextAmbient.Current;
            if (context is null) return Task.CompletedTask;

            var status = r.Succeeded
                ? $"✓ {r.ResponseStatusCode}"
                : $"✗ {r.ResponseStatusCode?.ToString() ?? "—"} {r.ErrorMessage}";

            var line = $"[{r.Id}] {status} | {r.ElapsedMs} ms" +
                       (r.CorrelationId is not null ? $" | corr={r.CorrelationId}" : "") +
                       (r.Channel       is not null ? $" | ch={r.Channel}"         : "") +
                       (r.Metadata      is not null ? $" | meta={r.Metadata}"      : "");

            context.WriteLine(line);
            return Task.CompletedTask;
        }
    }
}
