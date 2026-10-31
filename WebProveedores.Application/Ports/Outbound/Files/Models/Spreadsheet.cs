namespace WebProveedores.Application.Ports.Outbound.Files.Models;

/// <summary>Hoja de cálculo: cada fila trae un valor por columna, en el mismo orden.</summary>
public sealed record Spreadsheet(string SheetName, IReadOnlyList<SpreadsheetColumn> Columns, IReadOnlyList<IReadOnlyList<object?>> Rows);
