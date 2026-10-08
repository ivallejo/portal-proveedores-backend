using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Documents;

internal static class UblDocumentReader
{
    private static readonly Dictionary<string, string> DocumentTypes = new()
    {
        ["01"] = "Factura electrónica",
        ["03"] = "Boleta de venta electrónica",
        ["07"] = "Nota de crédito electrónica",
        ["08"] = "Nota de débito electrónica",
    };

    /// <summary>
    /// Lee el XML sin resolver DTD ni entidades externas (protección contra XXE).
    /// Devuelve <c>null</c> si el contenido no es un comprobante UBL reconocible.
    /// </summary>
    public static ElectronicDocument? Read(Stream content)
    {
        XElement root;
        try
        {
            using var reader = XmlReader.Create(content, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
            });
            root = XDocument.Load(reader).Root!;
        }
        catch (XmlException)
        {
            return null;
        }

        var kind = root.Name.LocalName;
        if (kind is not ("Invoice" or "CreditNote" or "DebitNote")) return null;

        var number = Text(root, "ID").ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(number) || !DateOnly.TryParse(Text(root, "IssueDate"), CultureInfo.InvariantCulture, out var issuedAt))
            return null;

        var typeCode = Text(root, "InvoiceTypeCode");
        if (string.IsNullOrEmpty(typeCode)) typeCode = kind == "CreditNote" ? "07" : kind == "DebitNote" ? "08" : "01";
        var (issuerRuc, issuerName) = Party(root, "AccountingSupplierParty");
        var (receiverRuc, receiverName) = Party(root, "AccountingCustomerParty");

        var lineName = kind == "CreditNote" ? "CreditNoteLine" : kind == "DebitNote" ? "DebitNoteLine" : "InvoiceLine";
        var quantityName = kind == "CreditNote" ? "CreditedQuantity" : kind == "DebitNote" ? "DebitedQuantity" : "InvoicedQuantity";
        var lines = Children(root, lineName).Select(line =>
        {
            var quantity = Amount(line, quantityName) is var q && q > 0 ? q : 1;
            var lineAmount = Amount(line, "LineExtensionAmount");
            var unitPrice = Amount(Child(line, "Price"), "PriceAmount") is var p && p > 0 ? p : lineAmount / quantity;
            var description = Text(Child(line, "Item"), "Description");
            if (string.IsNullOrEmpty(description)) description = Text(Child(line, "Item"), "Name");
            return new ElectronicDocumentLine(string.IsNullOrEmpty(description) ? "Ítem" : description, quantity, unitPrice, lineAmount > 0 ? lineAmount : quantity * unitPrice);
        }).ToList();

        var totals = Child(root, "LegalMonetaryTotal") ?? Child(root, "RequestedMonetaryTotal");
        decimal? igv = Child(root, "TaxTotal") is { } taxTotal ? Amount(taxTotal, "TaxAmount") : null;
        var subtotal = Amount(totals, "LineExtensionAmount");
        if (subtotal == 0) subtotal = lines.Sum(line => line.Amount);
        var total = Amount(totals, "PayableAmount");
        if (total == 0) total = subtotal + (igv ?? 0);

        var currencyCode = Text(root, "DocumentCurrencyCode");
        if (string.IsNullOrEmpty(currencyCode)) currencyCode = Child(totals, "PayableAmount")?.Attribute("currencyID")?.Value ?? "PEN";
        DateOnly? dueAt = DateOnly.TryParse(Text(root, "DueDate"), CultureInfo.InvariantCulture, out var due) ? due : null;

        return new ElectronicDocument(
            number,
            number.Split('-')[0],
            DocumentTypes.GetValueOrDefault(typeCode, "Comprobante electrónico"),
            issuerRuc,
            issuerName,
            receiverRuc,
            receiverName,
            issuedAt,
            dueAt,
            currencyCode.Equals("USD", StringComparison.OrdinalIgnoreCase) ? Currency.USD : Currency.PEN,
            lines,
            subtotal,
            igv,
            total);
    }

    private static (string Ruc, string Name) Party(XElement root, string role)
    {
        var party = Child(Child(root, role), "Party");
        var ruc = Text(Child(party, "PartyIdentification"), "ID");
        if (string.IsNullOrEmpty(ruc)) ruc = Text(Child(root, role), "CustomerAssignedAccountID");
        var name = Text(Child(party, "PartyLegalEntity"), "RegistrationName");
        if (string.IsNullOrEmpty(name)) name = Text(Child(party, "PartyName"), "Name");
        return (ruc, name);
    }

    private static IEnumerable<XElement> Children(XElement? parent, string name) =>
        parent?.Elements().Where(element => element.Name.LocalName == name) ?? [];

    private static XElement? Child(XElement? parent, string name) => Children(parent, name).FirstOrDefault();

    private static string Text(XElement? parent, string name) => Child(parent, name)?.Value.Trim() ?? string.Empty;

    private static decimal Amount(XElement? parent, string name) =>
        decimal.TryParse(Text(parent, name), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0;
}
