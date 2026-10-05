namespace WebProveedores.Application.Auth;

/// <summary>Regla única de contraseñas del portal (cambio, activación y contraseñas temporales del administrador).</summary>
public static class PasswordPolicy
{
    public const string Description = "La contraseña debe tener mínimo 8 caracteres, una mayúscula, una minúscula y un número.";

    public static bool IsSatisfiedBy(string? password) =>
        password is { Length: >= 8 } && password.Any(char.IsUpper) && password.Any(char.IsLower) && password.Any(char.IsDigit);
}
