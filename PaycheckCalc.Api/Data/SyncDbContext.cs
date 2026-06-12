using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PaycheckCalc.Api.Data;

/// <summary>
/// EF Core context backing both ASP.NET Core Identity (users, tokens) and the per-user saved
/// paychecks. Schema is created at startup via <c>EnsureCreated</c> (no migrations) — adequate for a
/// greenfield single-table-plus-identity store; schema changes would require recreating the database.
/// </summary>
public sealed class SyncDbContext(DbContextOptions<SyncDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<SavedPaycheckEntity> SavedPaychecks => Set<SavedPaycheckEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<SavedPaycheckEntity>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.NameKey });
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.PayloadJson).IsRequired();
        });
    }
}
