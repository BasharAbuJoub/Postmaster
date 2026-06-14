using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Postmaster.Core.Abstractions;
using Postmaster.EFCore.Manager;

namespace Postmaster;

/// <summary>
/// Extension methods for registering the Entity Framework Core storage provider.
/// </summary>
public static class EFCorePostmasterBuilderExtensions
{
    /// <summary>
    /// Registers Entity Framework Core as the Postmaster storage backend.
    /// Requires <see cref="ModelBuilderExtensions.UsePostmaster"/> to be called
    /// in your <c>DbContext.OnModelCreating</c> and a migration to be applied.
    /// </summary>
    /// <typeparam name="TContext">Your application's <see cref="DbContext"/> type.</typeparam>
    public static PostmasterBuilder UseEntityFrameworkCore<TContext>(
        this PostmasterBuilder builder)
        where TContext : DbContext
    {
        builder.Services.AddScoped<IOutboxStore, EfCoreOutboxStore<TContext>>();
        builder.Services.AddScoped<IOutboxManager, EfCoreOutboxManager<TContext>>();
        return builder;
    }
}
