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

        modelBuilder.Entity<Company>(entity =>
        {
            entity.ToTable("Companies");
            entity.HasKey(company => company.Id);
            entity.Property(company => company.Id).ValueGeneratedNever();
            entity.HasIndex(company => company.Code).IsUnique();
            entity.Property(company => company.Code).HasMaxLength(20).IsRequired();
            entity.Property(company => company.Name).HasMaxLength(200).IsRequired();
            entity.Property(company => company.Ruc).HasMaxLength(11);
        });

        modelBuilder.Entity<SupplierDocument>(entity =>
        {
            entity.ToTable("Documents");
            entity.HasKey(document => document.Id);
            entity.Property(document => document.Id).ValueGeneratedNever();
            entity.HasIndex(document => new { document.ProviderRuc, document.Number }).IsUnique();
            entity.HasIndex(document => new { document.Status, document.RegisteredAtUtc });
            entity.HasIndex(document => document.ApproverId);
            entity.HasIndex(document => document.RegisteredById);
            entity.Property(document => document.Number).HasMaxLength(30).IsRequired();
            entity.Property(document => document.EntryType).HasConversion<string>().HasMaxLength(30);
            entity.Property(document => document.DocumentType).HasMaxLength(80).IsRequired();
            entity.Property(document => document.ProviderRuc).HasMaxLength(11).IsRequired();
            entity.Property(document => document.ProviderName).HasMaxLength(200).IsRequired();
            entity.Property(document => document.ProviderEmail).HasMaxLength(320);
            entity.Property(document => document.Currency).HasConversion<string>().HasMaxLength(3);
            entity.Property(document => document.Subtotal).HasPrecision(18, 2);
            entity.Property(document => document.Igv).HasPrecision(18, 2);
            entity.Property(document => document.Amount).HasPrecision(18, 2);
            entity.Property(document => document.Concept).HasMaxLength(1000);
            entity.Property(document => document.RegisteredByName).HasMaxLength(250).IsRequired();
            entity.Property(document => document.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(document => document.RejectedBy).HasConversion<string>().HasMaxLength(20);
            entity.Property(document => document.IsPettyCash).HasDefaultValue(false);
            entity.Property(document => document.AreaName).HasMaxLength(120);
            entity.Property(document => document.ApproverName).HasMaxLength(200);
            entity.Property(document => document.ApproverEmail).HasMaxLength(320);
            entity.Property(document => document.ApprovalReferenceType).HasConversion<string>().HasMaxLength(10);
            entity.Property(document => document.ApprovalReference).HasMaxLength(30);
            entity.Property(document => document.OrderType).HasConversion<string>().HasMaxLength(10);
            entity.Property(document => document.OrderNumber).HasMaxLength(20);
            entity.Property(document => document.OrderBalance).HasPrecision(18, 2);
            entity.Property(document => document.OrderDescription).HasMaxLength(300);
            entity.Property(document => document.Validation).HasMaxLength(200);
            entity.HasOne(document => document.Company).WithMany().HasForeignKey(document => document.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AppUser>().WithMany().HasForeignKey(document => document.RegisteredById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AppUser>().WithMany().HasForeignKey(document => document.ApproverId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Area>().WithMany().HasForeignKey(document => document.AreaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(document => document.Items).WithOne().HasForeignKey(item => item.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(document => document.Attachments).WithOne().HasForeignKey(item => item.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(document => document.Events).WithOne().HasForeignKey(item => item.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DocumentItem>(entity =>
        {
            entity.ToTable("DocumentItems");
            entity.HasKey(item => item.Id);
            // Las claves se generan en el dominio: así EF agrega como nuevos los hijos añadidos a un documento existente.
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.Property(item => item.Description).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Quantity).HasPrecision(18, 4);
            entity.Property(item => item.UnitPrice).HasPrecision(18, 6);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<DocumentAttachment>(entity =>
        {
            entity.ToTable("DocumentAttachments");
            entity.HasKey(attachment => attachment.Id);
            entity.Property(attachment => attachment.Id).ValueGeneratedNever();
            entity.Property(attachment => attachment.Kind).HasConversion<string>().HasMaxLength(10);
            entity.Property(attachment => attachment.FileName).HasMaxLength(200).IsRequired();
            entity.Property(attachment => attachment.StorageKey).HasMaxLength(200).IsRequired();
            entity.Property(attachment => attachment.ContentType).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<DocumentEvent>(entity =>
        {
            entity.ToTable("DocumentEvents");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.HasIndex(item => new { item.DocumentId, item.Sequence }).IsUnique();
            entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Actor).HasMaxLength(300).IsRequired();
            entity.Property(item => item.Kind).HasConversion<string>().HasMaxLength(10);
            entity.Property(item => item.Note).HasMaxLength(1000);
        });
    }
}
