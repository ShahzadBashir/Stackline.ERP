using Microsoft.EntityFrameworkCore;
using Stackline.API.Auth;
using Stackline.API.Data.Entities;

namespace Stackline.API.Data;
public class MasterDbContext : DbContext
{
    private readonly ICurrentUserService _currentUserService;
    public MasterDbContext(DbContextOptions<MasterDbContext> options, ICurrentUserService currentUserService) : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<GlobalUser> GlobalUsers => Set<GlobalUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<GlobalUser>()
        .HasOne(user => user.Tenant)
        .WithMany(tenant => tenant.Users)
        .HasForeignKey(user => user.TenantId)
        .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Tenant>().HasIndex(t => t.CompanyName);
        modelBuilder.Entity<Tenant>().HasQueryFilter(t => !t.IsDeleted);

        modelBuilder.Entity<GlobalUser>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<GlobalUser>().HasQueryFilter(t => !t.IsDeleted);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker
            .Entries<BaseEntity>();

        var now = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:

                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = _currentUserService.UserId;

                    break;

                case EntityState.Modified:

                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = _currentUserService.UserId;

                    break;

                case EntityState.Deleted:

                    entry.State = EntityState.Modified;

                    entry.Entity.IsDeleted = true;
                    entry.Entity.IsActive = false;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = _currentUserService.UserId;

                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
