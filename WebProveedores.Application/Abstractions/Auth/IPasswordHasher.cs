namespace WebProveedores.Application.Abstractions.Auth;

/// <summary>Hash y verificación de contraseñas. El algoritmo es un detalle de infraestructura.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}
