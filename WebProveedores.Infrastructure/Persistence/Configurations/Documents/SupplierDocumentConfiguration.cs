using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Entities;

namespace WebProveedores.Infrastructure.Persistence.Configurations.Documents;

internal sealed class SupplierDocumentConfiguration : IEntityTypeConfiguration<SupplierDocument>
{
    public void Configure(EntityTypeBuilder<SupplierDocument> entity)
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
    }
}
