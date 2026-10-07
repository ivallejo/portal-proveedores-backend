using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using WebProveedores.Application.Abstractions.Providers;
using WebProveedores.Application.Common.Exceptions;

namespace WebProveedores.Infrastructure.Providers;

/// <summary>Adaptador de los servicios SAP zconsopago (órdenes de pago) y zconsfactu (estado de facturas).</summary>
public sealed class SapPaymentsClient(HttpClient httpClient, SapSettings settings) : ISapPaymentsGateway
{
    public async Task<IReadOnlyList<SapPaymentOrder>> FindPaymentOrdersAsync(string providerRuc, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var rows = await GetAsync<List<PaymentRow>>("zconsopago", providerRuc, from, to, cancellationToken);
        return rows.Select(row => new SapPaymentOrder(
            row.Opago,
            ParseDate(row.Zaldt) ?? ParseDate(row.Laufd),
            row.Zbukr,
            Clean(row.RucAdqui),
            Clean(row.NomAdqui),
            row.Stcd1,
            string.Join(' ', new[] { row.Name1, row.Name2 }.Where(part => !string.IsNullOrWhiteSpace(part))).Trim(),
            row.Waers,
            ParseAmount(row.Rwbtr),
            row.Rzawe,
            Clean(row.Banka),
            Clean(row.Ubknt),
            row.Vblnr,
            row.Detalle.Select(detail => new SapPaidDocument(
                ParseDocument(detail.Xblnr),
                ParseDate(detail.Bldat),
                ParseAmount(detail.Wrbtr),
                ParseAmount(detail.Qbshb),
                ParseAmount(detail.Detra),
                ParseAmount(detail.Rwbtr),
                RetentionDocument(detail),
                Clean(detail.DocDet?.ConstDetrac) ?? Clean(detail.InfoDetracc),
                Clean(detail.DocDet?.PorcDet))).ToArray())).ToArray();
    }

    public async Task<IReadOnlyList<SapInvoice>> FindInvoicesAsync(string providerRuc, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var rows = await GetAsync<List<InvoiceRow>>("zconsfactu", providerRuc, from, to, cancellationToken);
        return rows.Select(row => new SapInvoice(
            ParseDocument(row.Xblnr),
            row.Stcd1,
            Clean(row.RucAdqui),
            ParseAmount(row.Rwbtr),
            row.Waers,
            ParseDate(row.Bldat),
            IsMarked(row.Detra),
            IsMarked(row.Reten),
            row.Estad.Trim())).ToArray();
    }

    private async Task<T> GetAsync<T>(string service, string providerRuc, DateOnly from, DateOnly to, CancellationToken cancellationToken) where T : new()
    {
        var query = $"RUC={Uri.EscapeDataString(providerRuc)}&fechad={Uri.EscapeDataString(SapDate(from))}&fechah={Uri.EscapeDataString(SapDate(to))}";
        using var request = new HttpRequestMessage(HttpMethod.Get, settings.Url(service, query));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", settings.BasicToken);
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return new T();
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken) ?? new T();
        }
        // Red caída, VPN desconectada, tiempo agotado, circuito abierto o respuesta inesperada: SAP no está disponible.
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            throw new ServiceUnavailableException("No pudimos consultar SAP en este momento. Intenta nuevamente en unos minutos.", exception);
        }
    }

    /// <summary>«01-F008-00002578» → tipo 01 y número F008-00002578.</summary>
    internal static SapDocumentNumber ParseDocument(string value)
    {
        var text = value.Trim();
        var dash = text.IndexOf('-');
        return dash == 2 && char.IsAsciiDigit(text[0]) && char.IsAsciiDigit(text[1])
            ? new SapDocumentNumber(text[..2], text[3..])
            : new SapDocumentNumber(string.Empty, text);
    }

    /// <summary>SAP usa «yyyyMMdd» y «00000000» para fecha vacía.</summary>
    internal static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParseExact(value?.Trim(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    internal static decimal ParseAmount(string? value) =>
        decimal.TryParse(value?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ? amount : 0m;

    private static string SapDate(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static bool IsMarked(string? value) => string.Equals(value?.Trim(), "X", StringComparison.OrdinalIgnoreCase);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? RetentionDocument(PaymentDetailRow detail)
    {
        var retention = detail.DocRet;
        if (retention is not null && !string.IsNullOrWhiteSpace(retention.Numero))
            return string.IsNullOrWhiteSpace(retention.Serie) ? retention.Numero.Trim() : $"{retention.Serie.Trim()}-{retention.Numero.Trim()}";
        return Clean(detail.CompReten);
    }

    // ——— Formato JSON de SAP ———

    private sealed class PaymentRow
    {
        [JsonPropertyName("laufd")] public string? Laufd { get; init; }
        [JsonPropertyName("opago")] public string Opago { get; init; } = string.Empty;
        [JsonPropertyName("stcd1")] public string Stcd1 { get; init; } = string.Empty;
        [JsonPropertyName("name1")] public string? Name1 { get; init; }
        [JsonPropertyName("name2")] public string? Name2 { get; init; }
        [JsonPropertyName("zbukr")] public string Zbukr { get; init; } = string.Empty;
        [JsonPropertyName("vblnr")] public string Vblnr { get; init; } = string.Empty;
        [JsonPropertyName("waers")] public string Waers { get; init; } = string.Empty;
        [JsonPropertyName("zaldt")] public string? Zaldt { get; init; }
        [JsonPropertyName("rzawe")] public string Rzawe { get; init; } = string.Empty;
        [JsonPropertyName("banka")] public string? Banka { get; init; }
        [JsonPropertyName("ubknt")] public string? Ubknt { get; init; }
        [JsonPropertyName("rwbtr")] public string? Rwbtr { get; init; }
        [JsonPropertyName("ruc_adqui")] public string? RucAdqui { get; init; }
        [JsonPropertyName("nom_adqui")] public string? NomAdqui { get; init; }
        [JsonPropertyName("detalle")] public List<PaymentDetailRow> Detalle { get; init; } = [];
    }

    private sealed class PaymentDetailRow
    {
        [JsonPropertyName("xblnr")] public string Xblnr { get; init; } = string.Empty;
        [JsonPropertyName("bldat")] public string? Bldat { get; init; }
        [JsonPropertyName("wrbtr")] public string? Wrbtr { get; init; }
        [JsonPropertyName("qbshb")] public string? Qbshb { get; init; }
        [JsonPropertyName("detra")] public string? Detra { get; init; }
        [JsonPropertyName("rwbtr")] public string? Rwbtr { get; init; }
        [JsonPropertyName("comp_reten")] public string? CompReten { get; init; }
        [JsonPropertyName("info_detracc")] public string? InfoDetracc { get; init; }
        [JsonPropertyName("doc_ret")] public RetentionRow? DocRet { get; init; }
        [JsonPropertyName("doc_det")] public DetractionRow? DocDet { get; init; }
    }

    private sealed class RetentionRow
    {
        [JsonPropertyName("serie")] public string? Serie { get; init; }
        [JsonPropertyName("numero")] public string? Numero { get; init; }
    }

    private sealed class DetractionRow
    {
        [JsonPropertyName("const_detrac")] public string? ConstDetrac { get; init; }
        [JsonPropertyName("porc_det")] public string? PorcDet { get; init; }
    }

    private sealed class InvoiceRow
    {
        [JsonPropertyName("stcd1")] public string Stcd1 { get; init; } = string.Empty;
        [JsonPropertyName("xblnr")] public string Xblnr { get; init; } = string.Empty;
        [JsonPropertyName("rwbtr")] public string? Rwbtr { get; init; }
        [JsonPropertyName("waers")] public string Waers { get; init; } = string.Empty;
        [JsonPropertyName("bldat")] public string? Bldat { get; init; }
        [JsonPropertyName("detra")] public string? Detra { get; init; }
        [JsonPropertyName("reten")] public string? Reten { get; init; }
        [JsonPropertyName("estad")] public string Estad { get; init; } = string.Empty;
        [JsonPropertyName("ruc_adqui")] public string? RucAdqui { get; init; }
    }
}
