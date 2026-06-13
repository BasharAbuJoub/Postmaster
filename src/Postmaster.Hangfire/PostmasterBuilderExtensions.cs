using Microsoft.Extensions.DependencyInjection;
using Postmaster.Core.Abstractions;

namespace Postmaster.Hangfire
{
    /// <summary>
    /// Extension methods for integrating Postmaster with Hangfire.
    /// </summary>
    public static class PostmasterBuilderExtensions
    {
        /// <summary>
        /// Registers a Hangfire recurring job that drains the outbox on every
        /// <see cref="Postmaster.Core.PostmasterOptions.PollingInterval"/>.
        /// Requires Hangfire and a storage backend to already be configured in the application.
        /// </summary>
        public static PostmasterBuilder UseHangfire(this PostmasterBuilder builder)
        {
            builder.Services.AddHostedService<HangfireSchedulerService>();
            builder.Services.AddScoped<IOutboxEventHandler, PerformContextEventHandler>();
            return builder;
        }
    }
}
