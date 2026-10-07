using Microsoft.EntityFrameworkCore;
using WebProveedores.Domain.Identity;

namespace WebProveedores.Infrastructure.Persistence;

internal static class UserIncludes
{
    /// <summary>Correos, área, roles y sociedades: lo que define qué puede hacer el usuario.</summary>
    public static IQueryable<AppUser> WithAccess(IQueryable<AppUser> users) => users
        .Include(user => user.Emails)
        .Include(user => user.Area).ThenInclude(area => area!.Company)
        .Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role)
        .Include(user => user.UserCompanies).ThenInclude(userCompany => userCompany.Company);
}
