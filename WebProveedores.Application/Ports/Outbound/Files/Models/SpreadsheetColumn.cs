namespace WebProveedores.Application.Ports.Outbound.Files.Models;

public sealed record SpreadsheetColumn(string Header, SpreadsheetFormat Format = SpreadsheetFormat.Text);
