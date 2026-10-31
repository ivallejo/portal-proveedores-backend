using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebProveedores.Domain.Organization;

namespace WebProveedores.Infrastructure.Persistence.Configurations.Organization;

internal sealed class AreaCompanyConfiguration : IEntityTypeConfiguration<AreaCompany>
{
    public void Configure(EntityTypeBuilder<AreaCompany> entity)
    {
        entity.ToTable("AreaCompanies");
        entity.HasKey(item => new { item.AreaId, item.CompanyId });
        entity.HasIndex(item => item.CompanyId);
        entity.HasOne(item => item.Area).WithMany(area => area.Companies).HasForeignKey(item => item.AreaId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(item => item.Company).WithMany().HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}
