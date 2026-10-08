using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebProveedores.Domain.Access;

namespace WebProveedores.Infrastructure.Persistence.Configurations.Access;

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> entity)
    {
        entity.ToTable("UserRoles");
        entity.HasKey(userRole => new { userRole.UserId, userRole.RoleId });
        entity.HasOne(userRole => userRole.User).WithMany(user => user.UserRoles).HasForeignKey(userRole => userRole.UserId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(userRole => userRole.Role).WithMany(role => role.UserRoles).HasForeignKey(userRole => userRole.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}
