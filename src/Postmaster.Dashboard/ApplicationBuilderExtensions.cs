using Microsoft.AspNetCore.Builder;
using Postmaster.Dashboard;

namespace Postmaster
{
    /// <summary>
    /// Extension methods for registering the Postmaster dashboard.
    /// </summary>
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Mounts the Postmaster dashboard at the specified path prefix.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="pathPrefix">The URL path prefix for the dashboard. Defaults to <c>/postmaster</c>.</param>
        /// <param name="configure">Optional delegate to configure <see cref="DashboardOptions"/>.</param>
        public static IApplicationBuilder UsePostmasterDashboard(
            this IApplicationBuilder app,
            string pathPrefix = "/postmaster",
            Action<DashboardOptions>? configure = null)
        {
            var options = new DashboardOptions();
            configure?.Invoke(options);
            return app.UseMiddleware<DashboardMiddleware>(pathPrefix, options);
        }
    }
}
