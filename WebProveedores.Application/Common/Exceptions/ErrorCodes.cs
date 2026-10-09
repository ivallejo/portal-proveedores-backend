namespace WebProveedores.Application.Common.Exceptions;

/// <summary>Códigos de error que la API devuelve en el campo «code» y que el frontend reconoce.</summary>
public static class ErrorCodes
{
    /// <summary>El RUC es de un proveedor en SAP, pero no tiene correo: no se puede enviar el enlace de activación.</summary>
    public const string ProviderEmailMissing = "PROVIDER_EMAIL_MISSING";
}
