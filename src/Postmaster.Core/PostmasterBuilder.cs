using Microsoft.Extensions.DependencyInjection;
using Postmaster.Core;
using Postmaster.Core.Processor;

namespace Postmaster
{
    /// <summary>
    /// Fluent builder for configuring the Postmaster outbox.
    /// Obtain an instance via <see cref="ServiceCollectionExtensions.AddPostmaster"/>.
    /// </summary>
    public class PostmasterBuilder
    {
        /// <summary>
        /// The underlying service collection. Use this to register additional services
        /// when building custom storage providers or processor hosts.
        /// </summary>
        public IServiceCollection Services { get; }

        internal PostmasterBuilder(IServiceCollection services)
        {
            Services = services;
        }

        /// <summary>
        /// Configures <see cref="PostmasterOptions"/>.
        /// </summary>
        public PostmasterBuilder Configure(Action<PostmasterOptions> configure)
        {
            Services.Configure(configure);
            return this;
        }

        /// <summary>
        /// Registers the built-in <see cref="BackgroundService"/> as the processor host.
        /// It polls the outbox on the configured <see cref="PostmasterOptions.PollingInterval"/>
        /// and dispatches batches continuously while messages are pending.
        /// Omit this call if you are using an alternative processor host.
        /// </summary>
        public PostmasterBuilder UseBackgroundService()
        {
            Services.AddHostedService<OutboxProcessorHost>();
            return this;
        }
    }
}
