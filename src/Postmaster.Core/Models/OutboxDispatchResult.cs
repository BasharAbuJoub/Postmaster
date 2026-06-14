namespace Postmaster
{
    /// <summary>
    /// The outcome of dispatching a single outbox message.
    /// Passed to the <c>onDispatched</c> callback in
    /// <see cref="Postmaster.Core.Abstractions.IOutboxProcessor.ProcessAsync"/>.
    /// </summary>
    public sealed record OutboxDispatchResult
    {
        /// <summary>The message ID.</summary>
        public Guid Id { get; init; }

        /// <summary>The correlation ID, if any.</summary>
        public string? CorrelationId { get; init; }

        /// <summary>The channel the message belongs to, if any.</summary>
        public string? Channel { get; init; }

        /// <summary>Arbitrary metadata attached to the message, if any.</summary>
        public string? Metadata { get; init; }

        /// <summary>Whether the HTTP request succeeded.</summary>
        public bool Succeeded { get; init; }

        /// <summary>The HTTP response status code, if a response was received.</summary>
        public int? ResponseStatusCode { get; init; }

        /// <summary>How long the HTTP request took in milliseconds.</summary>
        public long? ElapsedMs { get; init; }

        /// <summary>The error message if the dispatch failed.</summary>
        public string? ErrorMessage { get; init; }
    }
}
