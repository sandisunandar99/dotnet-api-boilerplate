using App.Models;
using Microsoft.EntityFrameworkCore;

namespace App.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure the one-to-many relationship between Role and User
        modelBuilder.Entity<Role>()
            .HasMany(r => r.Users)
            .WithOne(u => u.Role)
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict); // Prevents cascade delete

        // Configure the one-to-many relationship between Role and Permission
        modelBuilder.Entity<Role>()
            .HasMany(r => r.Permissions)
            .WithOne(p => p.Role)
            .HasForeignKey(p => p.RoleId)
            .OnDelete(DeleteBehavior.Cascade); // Cascade delete permissions when a role is deleted

        // Seed some initial roles (optional but recommended) - Using fixed dates for reproducibility
        var baseDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "User", Description = "Regular user", CreatedAt = baseDate.AddDays(1) },
            new Role { Id = 2, Name = "Guest", Description = "Guest user", CreatedAt = baseDate.AddDays(2) },
            new Role { Id = 99, Name = "Admin", Description = "Administrator", CreatedAt = baseDate.AddDays(3) }
        );

        // Seed some initial permissions (optional) - Using fixed dates for reproducibility
        modelBuilder.Entity<Permission>().HasData(
            new Permission { Id = 1, RoleId = 99, Name = "Manage Users", Description = "Can create, update, delete users", CreatedAt = baseDate.AddDays(4) },
            new Permission { Id = 2, RoleId = 99, Name = "Manage Roles", Description = "Can create, update, delete roles", CreatedAt = baseDate.AddDays(5) }
        );
    }
}
