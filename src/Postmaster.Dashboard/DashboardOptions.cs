namespace Postmaster.Dashboard
{
    /// <summary>
    /// Configuration options for the Postmaster dashboard middleware.
    /// </summary>
    public class DashboardOptions
    {
        /// <summary>
        /// Username for HTTP Basic authentication. When <c>null</c>, the dashboard is accessible without authentication.
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Password for HTTP Basic authentication.
        /// </summary>
        public string? Password { get; set; }
    }
}
