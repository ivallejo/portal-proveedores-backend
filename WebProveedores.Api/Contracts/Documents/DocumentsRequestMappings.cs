using WebProveedores.Application.Documents.Commands;

namespace WebProveedores.Api.Contracts.Documents;

/// <summary>Traduce los requests HTTP de Documents a los commands de sus casos de uso.</summary>
internal static class DocumentsRequestMappings
{
    public static ValidateOrderCommand ToCommand(this ValidateOrderRequest request) => new() { CompanyCode = request.CompanyCode, OrderType = request.OrderType, Number = request.Number };

    public static ApproveDocumentCommand ToCommand(this ApproveDocumentRequest request) => new() { ReferenceType = request.ReferenceType, Reference = request.Reference };

    public static RejectDocumentCommand ToCommand(this RejectDocumentRequest request) => new() { Reason = request.Reason };

    public static ReassignDocumentCommand ToCommand(this ReassignDocumentRequest request) => new() { ApproverId = request.ApproverId, Reason = request.Reason };

    public static ObserveDocumentCommand ToCommand(this ObserveDocumentRequest request) => new() { Reason = request.Reason, Email = request.Email };
}
