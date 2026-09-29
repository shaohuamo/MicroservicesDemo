using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using ProductsMicroservice.Infrastructure.DbContext;
using ProductsMicroservice.Infrastructure.SeedData;

namespace ProductsMicroService.API.Extensions;

/// <summary>
/// Database migration extensions for the Products API.
/// </summary>
public static class DatabaseMigrationExtensions
{
    /// <summary>
    /// Applies pending database migrations and seeds development data.
    /// </summary>
    /// <param name="app">The Products API application.</param>
    /// <returns>A task representing the asynchronous migration operation.</returns>
    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        ILogger logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(DatabaseMigrationExtensions));
        ApplicationDbContext dbContext;

        try
        {
            logger.LogInformation("Applying Products database migrations.");
            dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await dbContext.Database.MigrateAsync();
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Products database migration failed during startup.");
            throw;
        }

        try
        {
            logger.LogInformation("Seeding Products database.");
            await ProductsSeedData.SeedAsync(
                dbContext,
                scope.ServiceProvider.GetRequiredService<IDistributedLockProvider>());
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Products database seed failed during startup.");
            throw;
        }
    }
}
