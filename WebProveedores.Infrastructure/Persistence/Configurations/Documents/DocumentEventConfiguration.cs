using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Entities;

namespace WebProveedores.Infrastructure.Persistence.Configurations.Documents;

internal sealed class DocumentEventConfiguration : IEntityTypeConfiguration<DocumentEvent>
{
    public void Configure(EntityTypeBuilder<DocumentEvent> entity)
    {
        entity.ToTable("DocumentEvents");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).ValueGeneratedNever();
        entity.HasIndex(item => new { item.DocumentId, item.Sequence }).IsUnique();
        entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
        entity.Property(item => item.Actor).HasMaxLength(300).IsRequired();
        entity.Property(item => item.Kind).HasConversion<string>().HasMaxLength(10);
        entity.Property(item => item.Note).HasMaxLength(1000);
    }
}
