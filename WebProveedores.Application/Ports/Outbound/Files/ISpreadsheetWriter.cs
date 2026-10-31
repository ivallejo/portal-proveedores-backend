using WebProveedores.Application.Ports.Outbound.Files.Models;

namespace WebProveedores.Application.Ports.Outbound.Files;

/// <summary>Arma un archivo Excel (.xlsx) de una hoja con encabezados y filas.</summary>
public interface ISpreadsheetWriter
{
    byte[] Write(Spreadsheet sheet);
}
