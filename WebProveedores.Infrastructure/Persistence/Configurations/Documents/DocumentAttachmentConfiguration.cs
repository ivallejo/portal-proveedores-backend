using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebProveedores.Domain.Documents;

namespace WebProveedores.Infrastructure.Persistence.Configurations.Documents;

internal sealed class DocumentAttachmentConfiguration : IEntityTypeConfiguration<DocumentAttachment>
{
    public void Configure(EntityTypeBuilder<DocumentAttachment> entity)
    {
        entity.ToTable("DocumentAttachments");
        entity.HasKey(attachment => attachment.Id);
        entity.Property(attachment => attachment.Id).ValueGeneratedNever();
        entity.Property(attachment => attachment.Kind).HasConversion<string>().HasMaxLength(10);
        entity.Property(attachment => attachment.FileName).HasMaxLength(200).IsRequired();
        entity.Property(attachment => attachment.StorageKey).HasMaxLength(200).IsRequired();
        entity.Property(attachment => attachment.ContentType).HasMaxLength(100).IsRequired();
    }
}
