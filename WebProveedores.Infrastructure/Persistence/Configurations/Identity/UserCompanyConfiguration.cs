using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebProveedores.Domain.Organization;

namespace WebProveedores.Infrastructure.Persistence.Configurations.Identity;

internal sealed class UserCompanyConfiguration : IEntityTypeConfiguration<UserCompany>
{
    public void Configure(EntityTypeBuilder<UserCompany> entity)
    {
        entity.ToTable("UserCompanies");
        entity.HasKey(userCompany => new { userCompany.UserId, userCompany.CompanyId });
        entity.HasIndex(userCompany => userCompany.CompanyId);
        entity.HasOne(userCompany => userCompany.User).WithMany(user => user.UserCompanies).HasForeignKey(userCompany => userCompany.UserId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(userCompany => userCompany.Company).WithMany().HasForeignKey(userCompany => userCompany.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}
