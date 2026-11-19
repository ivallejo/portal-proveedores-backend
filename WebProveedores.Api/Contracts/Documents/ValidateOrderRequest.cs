using System.ComponentModel.DataAnnotations;
using WebProveedores.Domain.Documents;

namespace WebProveedores.Api.Contracts.Documents;

public sealed class ValidateOrderRequest
{
    [Required, MaxLength(20)] public string CompanyCode { get; init; } = string.Empty;
    [Required] public OrderType OrderType { get; init; }
    /// <summary>Solo en órdenes de servicio: OnBoard (por defecto) o Warehouse.</summary>
    public DeliveryType? DeliveryType { get; init; }
    [Required, MaxLength(20)] public string Number { get; init; } = string.Empty;
}
