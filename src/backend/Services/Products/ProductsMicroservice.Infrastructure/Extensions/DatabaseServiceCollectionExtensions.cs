using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Infrastructure.DbContext;
using ProductsMicroservice.Infrastructure.Options;
using ProductsMicroservice.Infrastructure.Repositories;

namespace ProductsMicroservice.Infrastructure.Extensions
{
    internal static class DatabaseServiceCollectionExtensions
    {
        internal static IServiceCollection AddProductsDatabase(this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<PostgresOptions>(configuration.GetSection(PostgresOptions.SectionName));

            services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
            {
                var env = serviceProvider.GetRequiredService<IHostEnvironment>();
                var postgresOptions = serviceProvider.GetRequiredService<IOptions<PostgresOptions>>().Value;
                string connectionStringTemplate = configuration.GetConnectionString("PostgresConnection")!;
                string connectionString = connectionStringTemplate
                    .Replace("$POSTGRES_HOST", postgresOptions.Host)
                    .Replace("$POSTGRES_PASSWORD", postgresOptions.Password)
                    .Replace("$POSTGRES_DATABASE", postgresOptions.Database)
                    .Replace("$POSTGRES_PORT", postgresOptions.Port)
                    .Replace("$POSTGRES_USER", postgresOptions.User);

                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: postgresOptions.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(postgresOptions.MaxRetryDelaySeconds),
                        errorCodesToAdd: null);
                });

                if (env.IsDevelopment())
                {
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
                }
            });

            services.AddScoped<IUnitOfWork>(serviceProvider =>
                serviceProvider.GetRequiredService<ApplicationDbContext>());
            services.AddScoped<IProductsRepository, ProductsRepository>();
            services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();

            return services;
        }
    }
}
