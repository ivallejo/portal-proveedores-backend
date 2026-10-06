using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WebProveedores.Application;
using WebProveedores.Application.Abstractions;
using WebProveedores.Application.Abstractions.Providers;
using WebProveedores.Application.Documents;
using WebProveedores.Application.Payments;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Entities;
using WebProveedores.Infrastructure.Persistence;
using WebProveedores.Infrastructure.Providers;

namespace WebProveedores.Tests;

public sealed class PaymentQueryTests
{
    private const string ProviderRuc = "20100000001";
    private static readonly DateOnly From = new(2026, 1, 1);
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
             "info_detracc":"316991800","doc_ret":{"serie":"","numero":""},"doc_det":{"const_detrac":"316991800"}}]}]
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

        Assert.Contains("/sap/bc/zconsopago?sap-client=200&RUC=20100000001&fechad=01%2F01%2F2026&fechah=05%2F10%2F2026", handler.LastUrl);
        Assert.Equal("Basic", handler.LastAuthorization);
        var order = Assert.Single(orders);
        Assert.Equal(("1002", 426.43m, "T", new DateOnly(2026, 5, 21)), (order.CompanyCode, order.Total, order.PaymentMethodCode, order.PaidAt!.Value));
        var (invoice, debitNote) = (order.Documents[0], order.Documents[1]);
        Assert.Equal(new SapDocumentNumber("01", "F008-00002578"), invoice.Document);
        Assert.Equal(("R001-00001234", 7.08m, 228.92m), (invoice.RetentionDocument!, invoice.Retention, invoice.Paid));
        Assert.Equal(("08", "316991800", 27.03m), (debitNote.Document.TypeCode, debitNote.DetractionCertificate!, debitNote.Detraction));
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

        var orders = await fixture.Service.SearchPaymentOrdersAsync(fixture.Provider.Id, new PaymentSearchRequest("20999999999", null, From, To), CancellationToken.None);

        Assert.Equal(ProviderRuc, fixture.Sap.LastRuc);
        Assert.Equal(["Factura", "Nota de débito"], Assert.Single(orders).Documents.Select(document => document.Type));
        Assert.Equal(("Sociedad SAP", "Transferencia bancaria"), (orders[0].CompanyName, orders[0].PaymentMethod));
    }

    [Fact]
    public async Task Accounts_payable_must_indicate_the_ruc_and_only_sees_its_companies()
    {
        await using var fixture = await Fixture.CreateAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Service.SearchPaymentOrdersAsync(fixture.Accounting.Id, new PaymentSearchRequest(null, null, From, To), CancellationToken.None));
        // CxP solo tiene Naviera (1001): la orden de 1002 no se muestra.
        Assert.Empty(await fixture.Service.SearchPaymentOrdersAsync(fixture.Accounting.Id, new PaymentSearchRequest(ProviderRuc, null, From, To), CancellationToken.None));
        // La factura de una sociedad sin RUC configurado se muestra igual; la de Naviera (RUC conocido) también.
        var invoices = await fixture.Service.SearchInvoicesAsync(fixture.Accounting.Id, new InvoiceSearchRequest(ProviderRuc, null, null, From, To), CancellationToken.None);
        Assert.Equal(2, invoices.Count);
        Assert.Equal("Naviera Transoceánica", invoices.Single(item => item.CompanyRuc == "20100126606").CompanyName);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            fixture.Service.SearchInvoicesAsync(fixture.Internal.Id, new InvoiceSearchRequest(ProviderRuc, null, null, From, To), CancellationToken.None));
    }

    [Fact]
    public async Task Invoice_filters_validate_dates_and_need_the_company_ruc()
    {
        await using var fixture = await Fixture.CreateAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Service.SearchInvoicesAsync(fixture.Provider.Id, new InvoiceSearchRequest(null, null, null, To, From), CancellationToken.None));
        await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Service.SearchInvoicesAsync(fixture.Provider.Id, new InvoiceSearchRequest(null, null, null, new DateOnly(2020, 1, 1), To), CancellationToken.None));
        var noRuc = await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Service.SearchInvoicesAsync(fixture.Provider.Id, new InvoiceSearchRequest(null, "1002", null, From, To), CancellationToken.None));
        Assert.Contains("no tiene RUC", noRuc.Message);

        var byNumber = await fixture.Service.SearchInvoicesAsync(fixture.Provider.Id, new InvoiceSearchRequest(null, "1001", "f008-0000836", From, To), CancellationToken.None);
        Assert.Equal("F008-00008365", Assert.Single(byNumber).Number);
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

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(AppDbContext db) => Db = db;

        public AppDbContext Db { get; }
        public RecordingSap Sap { get; } = new();
        public IPaymentQueryService Service { get; private set; } = null!;
        public AppUser Provider { get; private set; } = null!;
        public AppUser Accounting { get; private set; } = null!;
        public AppUser Internal { get; private set; } = null!;

        public static async Task<Fixture> CreateAsync()
        {
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"payments-{Guid.NewGuid():N}").Options);
            var fixture = new Fixture(db);
            var roles = SecurityCatalog.Roles.ToDictionary(role => role.Key, role => new Role { Code = role.Key, Name = role.Value });
            db.Roles.AddRange(roles.Values);
            // Naviera con RUC conocido; Ultratag (1002) todavía sin RUC.
            var naviera = new Company { Code = "1001", Name = "Naviera Transoceánica", Ruc = "20100126606" };
            var ultratag = new Company { Code = "1002", Name = "Ultratag" };
            db.Companies.AddRange(naviera, ultratag);

            AppUser User(string username, string role, string? ruc, params Company[] companies)
            {
                var user = AppUser.Create(username, username, ruc, $"{username}@test.pe", "x", DateTime.UtcNow);
                user.SetRoles([roles[role]]);
                user.SetCompanies(companies);
                db.Users.Add(user);
                return user;
            }

            fixture.Provider = User("proveedor", SecurityCatalog.ProviderRole, ProviderRuc, naviera, ultratag);
            fixture.Accounting = User("cxp", SecurityCatalog.AccountsPayableRole, null, naviera);
            fixture.Internal = User("interno", SecurityCatalog.InternalUserRole, null, naviera);
            await db.SaveChangesAsync();

            var access = new DocumentAccess(new EfDocumentRepository(db), new EfUserRepository(db));
            fixture.Service = new PaymentQueryService(fixture.Sap, access, new EfReferenceDataReader(db));
            return fixture;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
