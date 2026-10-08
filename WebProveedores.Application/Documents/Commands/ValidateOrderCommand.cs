using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Documents.Commands;

public sealed record ValidateOrderCommand
{
    public string CompanyCode { get; init; } = string.Empty;
    public OrderType OrderType { get; init; }
    public string Number { get; init; } = string.Empty;
}
