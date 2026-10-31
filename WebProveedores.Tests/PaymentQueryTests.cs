using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Application.Payments;
using WebProveedores.Application.Payments.Queries;
using WebProveedores.Application.Ports.Outbound.Files;
using WebProveedores.Application.Ports.Outbound.Files.Models;
using WebProveedores.Application.Ports.Outbound.Sap;
using WebProveedores.Application.Ports.Outbound.Sap.Models;
using WebProveedores.Domain.Access;
using WebProveedores.Domain.Identity;
using WebProveedores.Domain.Organization;
using WebProveedores.Infrastructure.Files;
using WebProveedores.Infrastructure.Persistence;
using WebProveedores.Infrastructure.Persistence.Repositories;
using WebProveedores.Infrastructure.Sap;

namespace WebProveedores.Tests;

public sealed class PaymentQueryTests
{
    private const string ProviderRuc = "20100000001";
    // Rango máximo: 31 días contando ambos extremos.
    private static readonly DateOnly From = new(2026, 9, 5);
    private static readonly DateOnly To = new(2026, 10, 5);

    // Formato real de zconsopago (datos ficticios).
    private const string PaymentsJson = """
        [{"laufd":"20260521","laufi":"GIO02","opago":"20260521-GIO02","stcd1":"20100000001","name1":"PROVEEDOR DE PRUEBA SA","name2":"",
          "zbukr":"1002","vblnr":"3100000007","waers":"USD","zaldt":"20260521","rzawe":"T","hbkid":"09212","banka":"BANCO DE PRUEBA",
          "ubknt":"000C640797","zbnkn":"","banka_rec":"","rwbtr":"426.43","ruc_adqui":"20100126606","nom_adqui":"Sociedad SAP",
          "detalle":[
            {"xblnr":"01-F008-00002578","bldat":"20260201","wrbtr":"236.00","qbshb":"7.08","detra":"0.00","rwbtr":"228.92","comp_reten":"",
             "info_detracc":"","doc_ret":{"serie":"R001","numero":"00001234"},"doc_det":{"const_detrac":""}},
            {"xblnr":"08-F008-00010601","bldat":"00000000","wrbtr":"224.54","qbshb":"0.00","detra":"27.03","rwbtr":"197.51","comp_reten":"",
             "info_detracc":"316991800","doc_ret":{"serie":"","numero":""},"doc_det":{"const_detrac":"316991800","porc_det":"12.0 %"}}]}]
        """;

    // Formato real de zconsfactu (datos ficticios).
    private const string InvoicesJson = """
        [{"stcd1":"20100000001","xblnr":"01-F008-00008365","rwbtr":"209.56","waers":"USD","bldat":"20250217","detra":"X","reten":"","estad":"Pagado","ruc_adqui":"20100126606"},
         {"stcd1":"20100000001","xblnr":"07-F008-00000010","rwbtr":"17.70","waers":"PEN","bldat":"20250619","detra":"","reten":"X","estad":"Recepcionado","ruc_adqui":"20999999999"}]
        """;

    [Fact]
    public async Task Adapter_maps_the_sap_payment_format_and_sends_dates_as_dd_mm_yyyy()
    {
        var handler = new StubHandler(PaymentsJson);
        var orders = await Client(handler).FindPaymentOrdersAsync(ProviderRuc, From, To, CancellationToken.None);

        Assert.Contains("/sap/bc/zconsopago?sap-client=200&RUC=20100000001&fechad=05%2F09%2F2026&fechah=05%2F10%2F2026", handler.LastUrl);
        Assert.Equal("Basic", handler.LastAuthorization);
        var order = Assert.Single(orders);
        Assert.Equal(("1002", 426.43m, "T", new DateOnly(2026, 5, 21)), (order.CompanyCode, order.Total, order.PaymentMethodCode, order.PaidAt!.Value));
        var (invoice, debitNote) = (order.Documents[0], order.Documents[1]);
        Assert.Equal(new SapDocumentNumber("01", "F008-00002578"), invoice.Document);
        Assert.Equal(("R001-00001234", 7.08m, 228.92m), (invoice.RetentionDocument!, invoice.Retention, invoice.Paid));
        Assert.Equal(("08", "316991800", 27.03m, "12.0 %"), (debitNote.Document.TypeCode, debitNote.DetractionCertificate!, debitNote.Detraction, debitNote.DetractionRate!));
        Assert.Null(debitNote.IssuedAt);
    }

    [Fact]
    public async Task Adapter_maps_invoice_flags_and_reports_sap_failures_as_unavailable()
    {
        var invoices = await Client(new StubHandler(InvoicesJson)).FindInvoicesAsync(ProviderRuc, From, To, CancellationToken.None);
        Assert.Equal([(true, false, "Pagado"), (false, true, "Recepcionado")], invoices.Select(item => (item.HasDetraction, item.HasRetention, item.Status)));

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            Client(new StubHandler("error", HttpStatusCode.InternalServerError)).FindInvoicesAsync(ProviderRuc, From, To, CancellationToken.None));
    }

    [Fact]
    public async Task Provider_always_queries_its_own_ruc_and_sees_document_types()
    {
        await using var fixture = await Fixture.CreateAsync();

        var orders = await fixture.Service.SearchPaymentOrdersAsync(fixture.Provider.Id, new PaymentSearchQuery("20999999999", null, From, To), CancellationToken.None);

        Assert.Equal(ProviderRuc, fixture.Sap.LastRuc);
        Assert.Equal(["Factura", "Nota de débito"], Assert.Single(orders).Documents.Select(document => document.Type));
        Assert.Equal(("Sociedad SAP", "Transferencia bancaria"), (orders[0].CompanyName, orders[0].PaymentMethod));
        // «Todas las sociedades»: solo las del usuario; la factura de una sociedad que no está en el catálogo no se muestra.
        var invoices = await fixture.Service.SearchInvoicesAsync(fixture.Provider.Id, new InvoiceSearchQuery(null, null, null, From, To), CancellationToken.None);
        Assert.Equal("F008-00008365", Assert.Single(invoices).Number);
    }

    [Fact]
    public async Task Accounts_payable_must_indicate_the_ruc_and_only_sees_its_companies()
    {
        await using var fixture = await Fixture.CreateAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Service.SearchPaymentOrdersAsync(fixture.Accounting.Id, new PaymentSearchQuery(null, null, From, To), CancellationToken.None));
        // CxP solo tiene Naviera (1001): la orden de 1002 no se muestra.
        Assert.Empty(await fixture.Service.SearchPaymentOrdersAsync(fixture.Accounting.Id, new PaymentSearchQuery(ProviderRuc, null, From, To), CancellationToken.None));
        // Solo la factura de Naviera (RUC conocido y asignada); la de una sociedad no identificada en el catálogo no se muestra.
        var invoices = await fixture.Service.SearchInvoicesAsync(fixture.Accounting.Id, new InvoiceSearchQuery(ProviderRuc, null, null, From, To), CancellationToken.None);
        Assert.Equal("Naviera Transoceánica", Assert.Single(invoices).CompanyName);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            fixture.Service.SearchInvoicesAsync(fixture.Internal.Id, new InvoiceSearchQuery(ProviderRuc, null, null, From, To), CancellationToken.None));
    }

    [Fact]
    public async Task Invoice_filters_validate_dates_and_need_the_company_ruc()
    {
        await using var fixture = await Fixture.CreateAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Service.SearchInvoicesAsync(fixture.Provider.Id, new InvoiceSearchQuery(null, null, null, To, From), CancellationToken.None));
        await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Service.SearchInvoicesAsync(fixture.Provider.Id, new InvoiceSearchQuery(null, null, null, new DateOnly(2020, 1, 1), To), CancellationToken.None));
        var noRuc = await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Service.SearchInvoicesAsync(fixture.Provider.Id, new InvoiceSearchQuery(null, "1002", null, From, To), CancellationToken.None));
        Assert.Contains("no tiene RUC", noRuc.Message);

        var byNumber = await fixture.Service.SearchInvoicesAsync(fixture.Provider.Id, new InvoiceSearchQuery(null, "1001", "f008-0000836", From, To), CancellationToken.None);
        Assert.Equal("F008-00008365", Assert.Single(byNumber).Number);
    }

    [Fact]
    public async Task Range_is_at_most_one_month_counting_both_ends()
    {
        await using var fixture = await Fixture.CreateAsync();

        Assert.NotEmpty(await fixture.Service.SearchInvoicesAsync(fixture.Provider.Id, new InvoiceSearchQuery(null, null, null, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)), CancellationToken.None));
        var tooLong = await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Service.SearchPaymentOrdersAsync(fixture.Provider.Id, new PaymentSearchQuery(null, null, new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 31)), CancellationToken.None));
        Assert.Equal("El rango de fechas no puede superar 1 mes (31 días).", tooLong.Message);
        await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Service.ExportInvoicesAsync(fixture.Provider.Id, new InvoiceSearchQuery(null, null, null, new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 31)), CancellationToken.None));
    }

    [Fact]
    public async Task Export_has_one_row_per_paid_document_and_the_same_access_rules()
    {
        await using var fixture = await Fixture.CreateAsync();

        var file = await fixture.Service.ExportPaymentOrdersAsync(fixture.Provider.Id, new PaymentSearchQuery("20999999999", null, From, To), CancellationToken.None);

        Assert.Equal("ordenes-de-pago_20100000001_2026-09-05_2026-10-05.xlsx", file.FileName);
        var sheet = fixture.Spreadsheets.Last!;
        Assert.Equal(19, sheet.Columns.Count);
        Assert.Equal([("20260521-GIO02", "F008-00002578", 228.92m), ("20260521-GIO02", "F008-00010601", 197.51m)],
            sheet.Rows.Select(row => ((string)row[0]!, (string)row[10]!, (decimal)row[16]!)));

        var invoices = await fixture.Service.ExportInvoicesAsync(fixture.Provider.Id, new InvoiceSearchQuery(null, null, null, From, To), CancellationToken.None);
        Assert.Equal("estado-de-facturas_20100000001_2026-09-05_2026-10-05.xlsx", invoices.FileName);
        var row = Assert.Single(fixture.Spreadsheets.Last!.Rows);
        Assert.Equal(["F008-00008365", "Sí", "No", "Pagado"], new[] { row[0], row[7], row[8], row[9] });
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            fixture.Service.ExportInvoicesAsync(fixture.Internal.Id, new InvoiceSearchQuery(ProviderRuc, null, null, From, To), CancellationToken.None));
    }

    [Fact]
    public void ClosedXml_writer_keeps_text_as_text_and_formats_dates_and_amounts()
    {
        var sheet = new Spreadsheet("Hoja",
            [new("RUC"), new("Fecha", SpreadsheetFormat.Date), new("Importe", SpreadsheetFormat.Amount)],
            [["00012345678", new DateOnly(2026, 10, 5), 1234.5m], [null, null, null]]);

        using var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(new ClosedXmlSpreadsheetWriter().Write(sheet)));
        var worksheet = workbook.Worksheet("Hoja");

        Assert.Equal("RUC", worksheet.Cell(1, 1).GetString());
        Assert.True(worksheet.Cell(1, 1).Style.Font.Bold);
        Assert.Equal("00012345678", worksheet.Cell(2, 1).GetString());
        Assert.Equal(new DateTime(2026, 10, 5), worksheet.Cell(2, 2).GetDateTime());
        Assert.Equal(("dd/mm/yyyy", "#,##0.00"), (worksheet.Cell(2, 2).Style.NumberFormat.Format, worksheet.Cell(2, 3).Style.NumberFormat.Format));
        Assert.Equal(1234.5, worksheet.Cell(2, 3).GetDouble());
        Assert.True(worksheet.Cell(3, 1).IsEmpty());
    }

    private static SapPaymentsClient Client(StubHandler handler) =>
        new(new HttpClient(handler), new SapSettings("http://sap.invalid", "200", "test-token"));

    private sealed class StubHandler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string LastUrl { get; private set; } = string.Empty;
        public string? LastAuthorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUrl = request.RequestUri!.ToString();
            LastAuthorization = request.Headers.Authorization?.Scheme;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    /// <summary>Gateway con las respuestas del adaptador real sobre el JSON de ejemplo, registrando el RUC consultado.</summary>
    private sealed class RecordingSap : ISapPaymentsGateway
    {
        public string? LastRuc { get; private set; }

        public Task<IReadOnlyList<SapPaymentOrder>> FindPaymentOrdersAsync(string providerRuc, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            LastRuc = providerRuc;
            return Client(new StubHandler(PaymentsJson)).FindPaymentOrdersAsync(providerRuc, from, to, cancellationToken);
        }

        public Task<IReadOnlyList<SapInvoice>> FindInvoicesAsync(string providerRuc, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            LastRuc = providerRuc;
            return Client(new StubHandler(InvoicesJson)).FindInvoicesAsync(providerRuc, from, to, cancellationToken);
        }
    }

    /// <summary>Guarda la última hoja armada en lugar de generar el Excel.</summary>
    private sealed class RecordingSpreadsheets : ISpreadsheetWriter
    {
        public Spreadsheet? Last { get; private set; }

        public byte[] Write(Spreadsheet sheet)
        {
            Last = sheet;
            return [];
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(AppDbContext db) => Db = db;

        public AppDbContext Db { get; }
        public RecordingSap Sap { get; } = new();
        public RecordingSpreadsheets Spreadsheets { get; } = new();
        public IPaymentQueryService Service { get; private set; } = null!;
        public AppUser Provider { get; private set; } = null!;
        public AppUser Accounting { get; private set; } = null!;
        public AppUser Internal { get; private set; } = null!;

        public static async Task<Fixture> CreateAsync()
        {
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"payments-{Guid.NewGuid():N}").Options);
            var fixture = new Fixture(db);
            var roles = SecurityCatalog.Roles.ToDictionary(role => role.Key, role => new Role { Code = role.Key, Name = role.Value });
            TestMenus.Seed(db, roles);
            db.Roles.AddRange(roles.Values);
            // Naviera con RUC conocido; Petrolera (1002) todavía sin RUC.
            var naviera = new Company { Code = "1001", Name = "Naviera Transoceánica", Ruc = "20100126606" };
            var petrolera = new Company { Code = "1002", Name = "Petrolera Transoceánica" };
            db.Companies.AddRange(naviera, petrolera);

            AppUser User(string username, string role, string? ruc, params Company[] companies)
            {
                var user = AppUser.Create(username, username, ruc, $"{username}@test.pe", "x", DateTime.UtcNow);
                user.SetRoles([roles[role]]);
                user.SetCompanies(companies);
                db.Users.Add(user);
                return user;
            }

            fixture.Provider = User("proveedor", SecurityCatalog.ProviderRole, ProviderRuc, naviera, petrolera);
            fixture.Accounting = User("cxp", SecurityCatalog.AccountsPayableRole, null, naviera);
            fixture.Internal = User("interno", SecurityCatalog.InternalUserRole, null, naviera);
            await db.SaveChangesAsync();

            fixture.Service = new PaymentQueryService(fixture.Sap, TestServices.Actors(db), new EfCompanyReader(db), fixture.Spreadsheets);
            return fixture;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
