using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PaycheckCalc.Api.Data;

/// <summary>
/// EF Core context backing both ASP.NET Core Identity (users, tokens) and the per-user saved
/// paychecks. On PostgreSQL the schema is managed by EF Core migrations applied at startup (see
/// <c>Program.cs</c>), so it can evolve as new tables/columns are added; the SQLite-backed integration
/// tests build the schema directly from the model via <c>EnsureCreated</c>.
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
