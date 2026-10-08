namespace WebProveedores.Application.Ports.Outbound.Sap.Models;

/// <summary>Comprobante de SAP: tipo SUNAT (01 factura, 07 nota de crédito, 08 nota de débito…) y serie-número.</summary>
public sealed record SapDocumentNumber(string TypeCode, string Number);
