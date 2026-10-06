using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Entities;

namespace WebProveedores.Infrastructure.Persistence.Configurations.Identity;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> entity)
    {
        entity.ToTable("Companies");
        entity.HasKey(company => company.Id);
        entity.Property(company => company.Id).ValueGeneratedNever();
        entity.HasIndex(company => company.Code).IsUnique();
        entity.Property(company => company.Code).HasMaxLength(20).IsRequired();
        entity.Property(company => company.Name).HasMaxLength(200).IsRequired();
        entity.Property(company => company.Ruc).HasMaxLength(11);
        entity.Property(company => company.BillingEmail).HasMaxLength(320);
    }
}
