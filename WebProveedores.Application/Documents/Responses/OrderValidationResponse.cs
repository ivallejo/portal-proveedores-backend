using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Documents.Responses;

public sealed record OrderValidationResponse(string Number, OrderType OrderType, string Description, decimal Balance);
