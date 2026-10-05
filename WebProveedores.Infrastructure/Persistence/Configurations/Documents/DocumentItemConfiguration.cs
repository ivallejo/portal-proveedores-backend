using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Entities;

namespace WebProveedores.Infrastructure.Persistence.Configurations.Documents;

internal sealed class DocumentItemConfiguration : IEntityTypeConfiguration<DocumentItem>
{
    public void Configure(EntityTypeBuilder<DocumentItem> entity)
    {
        entity.ToTable("DocumentItems");
        entity.HasKey(item => item.Id);
        // Las claves se generan en el dominio: así EF agrega como nuevos los hijos añadidos a un documento existente.
        entity.Property(item => item.Id).ValueGeneratedNever();
        entity.Property(item => item.Description).HasMaxLength(500).IsRequired();
        entity.Property(item => item.Quantity).HasPrecision(18, 4);
        entity.Property(item => item.UnitPrice).HasPrecision(18, 6);
        entity.Property(item => item.Amount).HasPrecision(18, 2);
    }
}
