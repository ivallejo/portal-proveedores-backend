using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Domain.Common;
using WebProveedores.Domain.Identity;

namespace WebProveedores.Application.Common.Identity;

/// <summary>Códigos del documento en la API: RUC (proveedor), DNI, CE o PASAPORTE; «Usuario» si un interno antiguo no tiene documento.</summary>
internal static class UserDocuments
{
    public static (string Document, string Type) Of(AppUser user) =>
        user.IsProvider ? (user.Ruc!, "RUC")
        : user.DocumentType is { } type && user.DocumentNumber is { } number ? (number, Code(type))
        : (user.Username, "Usuario");

    public static string Code(IdentityDocumentType type) => type switch
    {
        IdentityDocumentType.Dni => "DNI",
        IdentityDocumentType.ForeignerCard => "CE",
        _ => "PASAPORTE",
    };

    /// <summary>Tipo y número validados; lanza <see cref="ValidationException"/> con el motivo.</summary>
    public static (IdentityDocumentType Type, string Number) Parse(string? code, string? number)
    {
        IdentityDocumentType type = (code?.Trim().ToUpperInvariant()) switch
        {
            "DNI" => IdentityDocumentType.Dni,
            "CE" => IdentityDocumentType.ForeignerCard,
            "PASAPORTE" => IdentityDocumentType.Passport,
            _ => throw new ValidationException("Elige el tipo de documento: DNI, carné de extranjería o pasaporte."),
        };
        try
        {
            return (type, IdentityDocument.Normalize(type, number));
        }
        catch (DomainRuleException exception)
        {
            throw new ValidationException(exception.Message);
        }
    }
}
