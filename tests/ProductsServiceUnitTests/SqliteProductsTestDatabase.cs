using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProductsMicroservice.Infrastructure.DbContext;

namespace ProductsServiceUnitTests;

internal sealed class SqliteProductsTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions<ApplicationDbContext> _options;

    private SqliteProductsTestDatabase()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
    }

    public static async Task<SqliteProductsTestDatabase> CreateAsync()
    {
        var database = new SqliteProductsTestDatabase();
        await using ApplicationDbContext dbContext = database.CreateContext();
        await dbContext.Database.EnsureCreatedAsync();
        return database;
    }

    public ApplicationDbContext CreateContext() => new(_options);

    public IServiceScopeFactory CreateScopeFactory()
    {
        ServiceProvider serviceProvider = new ServiceCollection()
            .AddScoped<ApplicationDbContext>(_ => CreateContext())
            .BuildServiceProvider();
        return serviceProvider.GetRequiredService<IServiceScopeFactory>();
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

}
