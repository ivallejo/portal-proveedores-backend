namespace WebProveedores.Application.Contracts.Documents.Responses;

public sealed record DocumentItemResponse(string Description, decimal Quantity, decimal UnitPrice, decimal Amount);
