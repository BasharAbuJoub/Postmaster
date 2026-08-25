using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Postmaster.Core;
using Postmaster.Core.Abstractions;
using Postmaster.Core.Processor;

namespace Postmaster
{
    /// <summary>
    /// Extension methods for registering Postmaster services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the core Postmaster services and returns a <see cref="PostmasterBuilder"/>
        /// for configuring the storage provider and processor host.
        /// </summary>
        /// <example>
        /// <code>
        /// builder.Services.AddPostmaster(postmaster =>
        /// {
        ///     postmaster.Configure(options => options.BatchSize = 20);
        ///     postmaster.UseEntityFrameworkCore&lt;AppDbContext&gt;();
        ///     postmaster.UseBackgroundService();
        /// });
        /// </code>
        /// </example>
        public static PostmasterBuilder AddPostmaster(
            this IServiceCollection services,
            Action<PostmasterBuilder>? configure = null)
        {
            services.AddOptions<PostmasterOptions>();
            services.AddHttpClient("Postmaster")
                .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
                {
                    var options = serviceProvider.GetRequiredService<IOptions<PostmasterOptions>>().Value;
                    var handler = new HttpClientHandler();

                    if (options.BypassSslCertificateValidation)
                        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

                    return handler;
                });
            services.AddScoped<IOutboxPublisher, OutboxPublisher>();
            services.AddSingleton<IOutboxProcessor, OutboxProcessor>();

            var builder = new PostmasterBuilder(services);
            configure?.Invoke(builder);
            return builder;
        }
    }
}
