using Microsoft.EntityFrameworkCore;
using Postmaster.EFCore.Configurations;

namespace Postmaster
{
    /// <summary>
    /// Extension methods for registering Postmaster entities with Entity Framework Core.
    /// </summary>
    public static class ModelBuilderExtensions
    {
        /// <summary>
        /// Registers the <c>OutboxMessage</c> entity configuration with the model builder.
        /// Call this inside your <c>DbContext.OnModelCreating</c> then add a migration.
        /// </summary>
        /// <param name="modelBuilder">The model builder.</param>
        /// <param name="tableName">The database table name. Default: <c>OutboxMessages</c>.</param>
        public static ModelBuilder UsePostmaster(this ModelBuilder modelBuilder, string tableName = "OutboxMessages")
        {
            modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration(tableName));

            return modelBuilder;
        }
    }
}
