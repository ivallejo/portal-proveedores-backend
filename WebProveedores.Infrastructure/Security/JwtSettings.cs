using Microsoft.Extensions.Configuration;

namespace WebProveedores.Infrastructure.Security;

/// <summary>Configuración del JWT (<c>Jwt:*</c>), validada al arrancar.</summary>
public sealed record JwtSettings(string SigningKey, string? Issuer, string? Audience, int AccessTokenMinutes)
{
    public static JwtSettings From(IConfiguration configuration)
    {
        var key = configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey no está configurado o tiene menos de 32 caracteres.");
        return new JwtSettings(key, configuration["Jwt:Issuer"], configuration["Jwt:Audience"], configuration.GetValue("Jwt:AccessTokenMinutes", 30));
    }
}
