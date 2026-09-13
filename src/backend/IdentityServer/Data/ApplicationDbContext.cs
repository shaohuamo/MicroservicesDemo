using IdentityServer.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Customize the ASP.NET Identity model and override the defaults if needed.
    // For example, you can rename the ASP.NET Identity table names and more.
    // Add your customizations after calling base.OnModelCreating(builder);
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Identity uses NormalizedEmail for FindByEmailAsync. The database constraint
        // closes the race between remote validation and concurrent registrations.
        builder.Entity<ApplicationUser>()
            .HasIndex(user => user.NormalizedEmail)
            .HasDatabaseName("EmailIndex")
            .IsUnique();
    }
    
}
