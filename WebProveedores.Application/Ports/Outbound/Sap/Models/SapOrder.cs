using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Ports.Outbound.Sap.Models;

public sealed record SapOrder(string Number, OrderType Type, string Description, decimal Balance);
