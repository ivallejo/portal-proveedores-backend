using WebProveedores.Application.Access.Responses;

namespace WebProveedores.Application.Access;

public interface INavigationService
{
    Task<IReadOnlyList<NavigationItem>> MenuForAsync(Guid userId, CancellationToken cancellationToken);
}
