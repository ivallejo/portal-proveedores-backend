namespace WebProveedores.Domain.Identity;

/// <summary>Estado de la cuenta: inactiva (no ingresa), bloqueada temporalmente por intentos fallidos o activa.</summary>
public enum UserStatus
{
    Active = 1,
    Inactive = 2,
    Locked = 3,
}
