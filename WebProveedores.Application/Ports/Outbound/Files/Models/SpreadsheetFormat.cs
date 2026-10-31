namespace WebProveedores.Application.Ports.Outbound.Files.Models;

/// <summary>Formato de la celda: texto, fecha (dd/mm/aaaa) o importe con 2 decimales.</summary>
public enum SpreadsheetFormat
{
    Text,
    Date,
    Amount,
}
