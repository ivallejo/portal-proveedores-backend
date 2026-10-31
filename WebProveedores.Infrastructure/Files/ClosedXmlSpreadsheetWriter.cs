using ClosedXML.Excel;
using WebProveedores.Application.Ports.Outbound.Files;
using WebProveedores.Application.Ports.Outbound.Files.Models;

namespace WebProveedores.Infrastructure.Files;

/// <summary>Excel con ClosedXML (licencia MIT): encabezados en negrita y fijos, filtros y columnas ajustadas.</summary>
public sealed class ClosedXmlSpreadsheetWriter : ISpreadsheetWriter
{
    public byte[] Write(Spreadsheet sheet)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheet.SheetName);

        for (var column = 0; column < sheet.Columns.Count; column++)
        {
            worksheet.Cell(1, column + 1).Value = sheet.Columns[column].Header;
        }
        for (var row = 0; row < sheet.Rows.Count; row++)
        {
            for (var column = 0; column < sheet.Columns.Count; column++)
            {
                var cell = worksheet.Cell(row + 2, column + 1);
                switch (sheet.Rows[row][column])
                {
                    case null:
                        break;
                    case DateOnly date:
                        cell.Value = date.ToDateTime(TimeOnly.MinValue);
                        break;
                    case decimal amount:
                        cell.Value = amount;
                        break;
                    case var value:
                        // Texto siempre como texto: RUC, cuentas y números de documento no deben perder ceros ni volverse números.
                        cell.Value = value.ToString();
                        break;
                }
            }
        }

        for (var column = 0; column < sheet.Columns.Count; column++)
        {
            var format = sheet.Columns[column].Format;
            if (format == SpreadsheetFormat.Text) continue;
            worksheet.Column(column + 1).Style.NumberFormat.Format = format == SpreadsheetFormat.Date ? "dd/mm/yyyy" : "#,##0.00";
        }

        var header = worksheet.Row(1);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F0FC");
        worksheet.SheetView.FreezeRows(1);
        worksheet.Range(1, 1, Math.Max(1, sheet.Rows.Count + 1), sheet.Columns.Count).SetAutoFilter();
        worksheet.Columns(1, sheet.Columns.Count).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
