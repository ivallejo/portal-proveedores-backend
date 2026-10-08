using WebProveedores.Application.Documents.Commands;
using WebProveedores.Application.Documents.Responses;

namespace WebProveedores.Application.Documents;

/// <summary>Registro de comprobantes electrónicos (Con OC / Sin OC) y documentos especiales.</summary>
public interface IDocumentRegistrationService
{
    Task<DocumentDetailResponse> RegisterAsync(Guid userId, RegisterElectronicDocumentCommand command, CancellationToken cancellationToken);
    Task<DocumentDetailResponse> RegisterSpecialAsync(Guid userId, RegisterSpecialDocumentCommand command, CancellationToken cancellationToken);
}
