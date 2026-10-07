using WebProveedores.Application.Ports.Outbound.Sap.Models;

namespace WebProveedores.Application.Ports.Outbound.Sap;

public interface IProviderDirectory
{
    Task<SapProviderRecord?> FindByRucAsync(string ruc, CancellationToken cancellationToken);
}
