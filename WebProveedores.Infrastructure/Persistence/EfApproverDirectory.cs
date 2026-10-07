using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Ports.Outbound.Persistence;
using WebProveedores.Application.Ports.Outbound.Persistence.Models;
using WebProveedores.Domain.Access;

namespace WebProveedores.Infrastructure.Persistence;

public sealed class EfApproverDirectory(AppDbContext db) : IApproverDirectory
{
    public async Task<IReadOnlyList<ApproverRecord>> ListApproversAsync(CancellationToken cancellationToken) =>
        await Approvers(null).ToListAsync(cancellationToken);

    public Task<ApproverRecord?> FindApproverAsync(Guid userId, CancellationToken cancellationToken) =>
        Approvers(userId).SingleOrDefaultAsync(cancellationToken);

    // El filtro por usuario va antes de la proyección para que SQL Server pueda traducirlo.
    private IQueryable<ApproverRecord> Approvers(Guid? userId) =>
        from user in db.Users
        where (userId == null || user.Id == userId)
              && user.IsActive
              && user.AreaId != null
              && user.Ruc == null
              // Aprobador: su rol (que no sea el de administrador) tiene la opción Documentos.
              && user.UserRoles.Any(userRole => userRole.Role.IsActive && userRole.Role.Code != SecurityCatalog.AdministratorRole
                  && userRole.Role.RoleMenus.Any(item => item.MenuOption.Code == MenuCatalog.Documents && item.MenuOption.IsActive))
        orderby user.CompanyName
        select new ApproverRecord(
            user.Id,
            user.CompanyName,
            user.Emails.Where(email => email.IsActive).OrderByDescending(email => email.IsPrimary).Select(email => email.Email).FirstOrDefault() ?? string.Empty,
            user.AreaId!.Value,
            user.Area!.Name,
            user.Area.Company.Code,
            user.UserCompanies.Select(userCompany => userCompany.Company.Code).ToList());
}
