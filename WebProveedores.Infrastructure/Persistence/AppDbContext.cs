using Microsoft.EntityFrameworkCore;
using WebProveedores.Domain.Entities;

namespace WebProveedores.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserEmail> UserEmails => Set<UserEmail>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(user => user.Id);
            entity.HasIndex(user => user.Username).IsUnique();
            entity.HasIndex(user => user.Ruc).IsUnique().HasFilter("[Ruc] IS NOT NULL");
            entity.HasIndex(user => user.AreaId);
            entity.Property(user => user.Username).HasMaxLength(80).IsRequired();
            entity.Property(user => user.CompanyName).HasMaxLength(200).IsRequired();
            entity.Property(user => user.Ruc).HasMaxLength(20);
            entity.Property(user => user.PasswordHash).HasMaxLength(500).IsRequired();
            entity.HasIndex(user => user.PasswordSetAtUtc);
            entity.HasOne(user => user.Area).WithMany(area => area.Users).HasForeignKey(user => user.AreaId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Area>(entity =>
        {
            entity.ToTable("Areas");
            entity.HasKey(area => area.Id);
            entity.HasIndex(area => area.Code).IsUnique();
            entity.Property(area => area.Code).HasMaxLength(50).IsRequired();
            entity.Property(area => area.Name).HasMaxLength(120).IsRequired();
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(role => role.Id);
            entity.HasIndex(role => role.Code).IsUnique();
            entity.Property(role => role.Code).HasMaxLength(50).IsRequired();
            entity.Property(role => role.Name).HasMaxLength(120).IsRequired();
            entity.Property(role => role.Description).HasMaxLength(300);
        });

        modelBuilder.Entity<UserEmail>(entity =>
        {
            entity.ToTable("UserEmails");
            entity.HasKey(email => email.Id);
            entity.HasIndex(email => email.Email).IsUnique();
            entity.HasIndex(email => new { email.UserId, email.IsPrimary }).HasFilter("[IsPrimary] = 1").IsUnique();
            entity.Property(email => email.Email).HasMaxLength(320).IsRequired();
            entity.HasOne(email => email.User).WithMany(user => user.Emails).HasForeignKey(email => email.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("UserRoles");
            entity.HasKey(userRole => new { userRole.UserId, userRole.RoleId });
            entity.HasOne(userRole => userRole.User).WithMany(user => user.UserRoles).HasForeignKey(userRole => userRole.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(userRole => userRole.Role).WithMany(role => role.UserRoles).HasForeignKey(userRole => userRole.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("PasswordResetTokens");
            entity.HasKey(token => token.Id);
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.HasIndex(token => new { token.UserId, token.ExpiresAtUtc });
            entity.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(token => token.Purpose).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.HasIndex(token => new { token.UserId, token.Purpose, token.ExpiresAtUtc });
            entity.HasOne(token => token.User).WithMany(user => user.PasswordResetTokens).HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
