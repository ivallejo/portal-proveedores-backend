using WebProveedores.Application.Access;

namespace WebProveedores.Application.Ports.Inbound.Access;

public interface INavigationService
{
    Task<IReadOnlyList<NavigationItem>> MenuForAsync(Guid userId, CancellationToken cancellationToken);
}
