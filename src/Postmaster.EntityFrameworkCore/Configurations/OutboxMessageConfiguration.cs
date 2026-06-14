using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Postmaster.Core.Entities;

namespace Postmaster.EFCore.Configurations
{
    public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
    {
        private readonly string _tableName;

        public OutboxMessageConfiguration(string tableName)
        {
            _tableName = tableName;
        }

        public void Configure(EntityTypeBuilder<OutboxMessage> builder)
        {
            builder.ToTable(_tableName);

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Url)
                .IsRequired()
                .HasMaxLength(2048);

            builder.Property(x => x.Method)
                .IsRequired()
                .HasMaxLength(10);

            builder.Property(x => x.Headers)
                .IsRequired()
                .HasMaxLength(4096);

            builder.Property(x => x.Payload)
                .IsRequired(false);

            builder.Property(x => x.Channel)
                .IsRequired(false)
                .HasMaxLength(256);

            builder.Property(x => x.ErrorMessage)
                .IsRequired(false);

            builder.Property(x => x.ResponseBody)
                .IsRequired(false);

            builder.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            builder.Property(x => x.Metadata)
                .IsRequired(false);

            builder.Property(x => x.LockedBy)
                .IsRequired(false)
                .HasMaxLength(50);

            builder.Property(x => x.LockedAt)
                .IsRequired(false);

            builder.Property(x => x.CorrelationId)
                .IsRequired()
                .HasMaxLength(256);

            builder.HasIndex(x => new { x.Status, x.NextAttemptAt, x.CreatedAt });
            builder.HasIndex(x => x.CorrelationId);
        }
    }
}
