namespace WebProveedores.Application.Ports.Outbound.Security;

/// <summary>Hash y verificación de contraseñas. El algoritmo es un detalle de infraestructura.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}
