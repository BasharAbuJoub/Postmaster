using Hangfire.Server;

namespace Postmaster.Hangfire
{
    /// <summary>
    /// Flows the current <see cref="PerformContext"/> through async call chains
    /// so scoped services (like event handlers) can access it without being
    /// constructed with it directly.
    /// </summary>
    internal static class PerformContextAmbient
    {
        private static readonly AsyncLocal<PerformContext?> _current = new();

        public static PerformContext? Current
        {
            get => _current.Value;
            set => _current.Value = value;
        }
    }
}
