using System.ComponentModel.DataAnnotations;
using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Contracts.Documents.Requests;

public sealed class ValidateOrderRequest
{
    [Required, MaxLength(20)] public string CompanyCode { get; init; } = string.Empty;
    [Required] public OrderType OrderType { get; init; }
    [Required, MaxLength(20)] public string Number { get; init; } = string.Empty;
}
