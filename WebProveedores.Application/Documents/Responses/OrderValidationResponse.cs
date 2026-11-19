using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Documents.Responses;

public sealed record OrderValidationResponse(string Number, OrderType OrderType, DeliveryType? DeliveryType, string Description, decimal Balance);
