using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.Exceptions;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Infrastructure.Messaging.Outbox;

namespace ProductsMicroservice.Infrastructure.DbContext;

public class ApplicationDbContext : Microsoft.EntityFrameworkCore.DbContext, IUnitOfWork
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products { get; set; }

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    internal DbSet<ProductOperationOutbox> ProductOperationOutbox =>
        Set<ProductOperationOutbox>();

    public void DiscardPendingChanges() => ChangeTracker.Clear();

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            Guid? productId = exception.Entries
                .Where(entry => entry.Entity is Product)
                .Select(entry => ((Product)entry.Entity).ProductId)
                .Cast<Guid?>()
                .FirstOrDefault();

            throw new ProductConcurrencyException(productId, exception);
        }
        catch (DbUpdateException exception) when (IsProductNameUniqueViolation(exception))
        {
            throw new ProductAlreadyExistsException(innerException: exception);
        }
        catch (DbUpdateException exception) when (IsIdempotencyUniqueViolation(exception))
        {
            throw new IdempotencyRecordConflictException(exception);
        }
    }

    private static bool IsProductNameUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException &&
        postgresException.SqlState == PostgresErrorCodes.UniqueViolation &&
        string.Equals(
            postgresException.ConstraintName,
            "IX_Products_ProductName",
            StringComparison.Ordinal);

    private static bool IsIdempotencyUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException &&
        postgresException.SqlState == PostgresErrorCodes.UniqueViolation &&
        string.Equals(
            postgresException.ConstraintName,
            "UX_IdempotencyRecords_User_Operation_Key",
            StringComparison.Ordinal);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products");
            entity.Property(product => product.ProductName)
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(product => product.DisplayName)
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(product => product.UnitPrice)
                .HasColumnType("numeric(10,2)");
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_Products_UnitPrice_Range",
                    """
                    "UnitPrice" >= 0.01 AND "UnitPrice" <= 99999999.99
                    """);
                table.HasCheckConstraint(
                    "CK_Products_QuantityInStock_Range",
                    """
                    "QuantityInStock" >= 0 AND "QuantityInStock" <= 1000000
                    """);
            });
            entity.Property(product => product.Version)
                .HasDefaultValue(1)
                .IsConcurrencyToken();
            entity.HasIndex(product => product.ProductName)
                .IsUnique();
        });

        modelBuilder.Entity<ProductOperationOutbox>(entity =>
        {
            entity.ToTable("ProductOperationOutbox");
            entity.HasKey(outbox => outbox.NotificationId);
            entity.Property(outbox => outbox.Payload).HasColumnType("jsonb");
            entity.Property(outbox => outbox.PayloadHash).HasMaxLength(64);
            entity.Property(outbox => outbox.TraceParent).HasMaxLength(512);
            entity.Property(outbox => outbox.TraceState).HasMaxLength(512);
            entity.Property(outbox => outbox.LastError).HasMaxLength(512);
            entity.Property(outbox => outbox.LockedBy).HasMaxLength(200);
            entity.Property(outbox => outbox.AttemptCount).HasDefaultValue(0);
            entity.Property(outbox => outbox.Version)
                .HasDefaultValue(0L)
                .IsConcurrencyToken();
            entity.HasIndex(outbox => new
            {
                outbox.PublishedAtUtc,
                outbox.NextAttemptAtUtc,
                outbox.LockedUntilUtc,
                outbox.CreatedAtUtc
            }).HasDatabaseName("IX_ProductOperationOutbox_Dispatch");
        });

        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.ToTable("IdempotencyRecords");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.UserId).HasMaxLength(256).IsRequired();
            entity.Property(record => record.Operation)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            entity.Property(record => record.RequestHash).HasMaxLength(64).IsRequired();
            entity.Property(record => record.ResponseJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(record => new
                {
                    record.UserId,
                    record.Operation,
                    record.IdempotencyKey
                })
                .IsUnique()
                .HasDatabaseName("UX_IdempotencyRecords_User_Operation_Key");
            entity.HasIndex(record => record.ExpiresAtUtc)
                .HasDatabaseName("IX_IdempotencyRecords_ExpiresAtUtc");
        });
    }
}
