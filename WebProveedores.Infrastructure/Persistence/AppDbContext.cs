using Microsoft.EntityFrameworkCore;
using WebProveedores.Domain.Entities;

namespace WebProveedores.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(user => user.Id);
            entity.HasIndex(user => user.Username).IsUnique().HasFilter("[Username] IS NOT NULL");
            entity.HasIndex(user => user.Email).IsUnique();
            entity.HasIndex(user => user.Ruc).IsUnique();
            entity.Property(user => user.Username).HasMaxLength(80);
            entity.Property(user => user.Email).HasMaxLength(320).IsRequired();
            entity.Property(user => user.CompanyName).HasMaxLength(200).IsRequired();
            entity.Property(user => user.Ruc).HasMaxLength(20).IsRequired();
            entity.Property(user => user.Area).HasMaxLength(100);
            entity.Property(user => user.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(user => user.PasswordResetTokenHash).HasMaxLength(128);
            entity.Property(user => user.Role).HasMaxLength(40).IsRequired();
        });
    }
}
