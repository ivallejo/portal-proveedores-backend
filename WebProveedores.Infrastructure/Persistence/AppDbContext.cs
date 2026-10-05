using Microsoft.EntityFrameworkCore;
using WebProveedores.Domain.Documents;
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
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<SupplierDocument> Documents => Set<SupplierDocument>();
    public DbSet<DocumentItem> DocumentItems => Set<DocumentItem>();
    public DbSet<DocumentAttachment> DocumentAttachments => Set<DocumentAttachment>();
    public DbSet<DocumentEvent> DocumentEvents => Set<DocumentEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
