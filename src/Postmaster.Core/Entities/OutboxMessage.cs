namespace Postmaster.Core.Entities
{
    public class OutboxMessage
    {
        public OutboxMessage()
        {
            Id = Guid.CreateVersion7();
            CreatedAt = DateTime.UtcNow;
        }

        public Guid Id { get; set; }

        public required string Url { get; set; }

        public required string Method { get; set; }

        public required string Headers { get; set; }

        public string? Payload { get; set; }

        public string? Channel { get; set; }

        public OutboxMessageStatus Status { get; set; }

        public int RetryCount { get; set; }

        public int MaxRetryCount { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime NextAttemptAt { get; set; }

        public DateTime? ProcessedAt { get; set; }

        public int? ResponseStatusCode { get; set; }

        public long? ElapsedMs { get; set; }

        public string? ResponseBody { get; set; }

        public string? ErrorMessage { get; set; }

        public string? Metadata { get; set; }

        public string? LockedBy { get; set; }

        public DateTime? LockedAt { get; set; }

        public required string CorrelationId { get; set; }
    }
}
