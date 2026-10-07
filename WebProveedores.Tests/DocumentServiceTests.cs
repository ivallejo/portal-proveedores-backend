using System.Text;
using Microsoft.EntityFrameworkCore;
using PdfSharp.Pdf;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Application.Documents;
using WebProveedores.Application.Ports.Outbound.Files;
using WebProveedores.Application.Ports.Outbound.Notifications;
using WebProveedores.Application.Ports.Outbound.Persistence.Models;
using WebProveedores.Domain.Access;
using WebProveedores.Domain.Common;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Identity;
using WebProveedores.Domain.Organization;
using WebProveedores.Infrastructure.Persistence;

namespace WebProveedores.Tests;

public sealed class DocumentServiceTests
{
    private const string ProviderRuc = "20512345678";
    private const string CompanyRuc = "20100000001";

    [Fact]
    public async Task RegisterAsync_without_order_sends_document_to_selected_approver()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Services.Registration.RegisterAsync(fixture.Provider.Id, Command(DocumentEntryType.WithoutPurchaseOrder, Xml("F001-00000100"), approverId: fixture.Approver.Id), CancellationToken.None);

        Assert.Equal(DocumentStatus.PendingApproval, result.Status);
        Assert.Equal("María Torres", result.ApproverName);
        Assert.Equal(3, result.Attachments.Count);
        Assert.Equal(1180m, result.Amount);
        Assert.Equal("Pendiente de aprobación", result.History[^1].Title);
        Assert.Equal("mtorres@test.pe", fixture.Email.Recipients.Single());
        Assert.Equal(3, fixture.Storage.Files.Count);
    }

    [Fact]
    public async Task RegisterAsync_with_order_goes_to_accounting_and_rejects_unknown_orders()
    {
        await using var fixture = await Fixture.CreateAsync();

        await Assert.ThrowsAsync<DocumentRejectedException>(() => fixture.Services.Registration.RegisterAsync(
            fixture.Provider.Id, Command(DocumentEntryType.WithPurchaseOrder, Xml("F001-00000101"), orderNumber: "4500099999"), CancellationToken.None));
        var result = await fixture.Services.Registration.RegisterAsync(
            fixture.Provider.Id, Command(DocumentEntryType.WithPurchaseOrder, Xml("F001-00000101"), orderNumber: "4500012873"), CancellationToken.None);

        Assert.Equal(DocumentStatus.PendingAccounting, result.Status);
        Assert.Equal("4500012873", result.OrderNumber);
        Assert.Equal("Orden validada en SAP", result.History[0].Title);
        // El intento con la orden inválida no debe dejar archivos guardados.
        Assert.Equal(3, fixture.Storage.Files.Count);
    }

    [Fact]
    public async Task RegisterAsync_rejects_xml_issued_by_another_ruc()
    {
        await using var fixture = await Fixture.CreateAsync();

        var exception = await Assert.ThrowsAsync<ValidationException>(() => fixture.Services.Registration.RegisterAsync(
            fixture.Provider.Id, Command(DocumentEntryType.WithoutPurchaseOrder, Xml("F001-00000102", issuerRuc: "20999999999"), approverId: fixture.Approver.Id), CancellationToken.None));

        Assert.Contains("20999999999", exception.Message);
        Assert.Empty(fixture.Storage.Files);
    }

    [Fact]
    public async Task RegisterAsync_requires_cdr_except_for_series_starting_with_e()
    {
        await using var fixture = await Fixture.CreateAsync();

        await Assert.ThrowsAsync<ValidationException>(() => fixture.Services.Registration.RegisterAsync(
            fixture.Provider.Id, Command(DocumentEntryType.WithoutPurchaseOrder, Xml("F001-00000103"), approverId: fixture.Approver.Id, includeCdr: false), CancellationToken.None));
        var result = await fixture.Services.Registration.RegisterAsync(
            fixture.Provider.Id, Command(DocumentEntryType.WithoutPurchaseOrder, Xml("E001-00000103"), approverId: fixture.Approver.Id, includeCdr: false), CancellationToken.None);

        Assert.DoesNotContain(result.Attachments, attachment => attachment.Kind == AttachmentKind.Cdr);
    }

    [Fact]
    public async Task RegisterAsync_rejects_duplicates_and_files_with_wrong_content()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Services.Registration.RegisterAsync(fixture.Provider.Id, Command(DocumentEntryType.WithoutPurchaseOrder, Xml("F001-00000104"), approverId: fixture.Approver.Id), CancellationToken.None);

        await Assert.ThrowsAsync<DocumentRejectedException>(() => fixture.Services.Registration.RegisterAsync(
            fixture.Provider.Id, Command(DocumentEntryType.WithoutPurchaseOrder, Xml("F001-00000104"), approverId: fixture.Approver.Id), CancellationToken.None));
        var fakePdf = Command(DocumentEntryType.WithoutPurchaseOrder, Xml("F001-00000105"), approverId: fixture.Approver.Id) with { Pdf = File("factura.pdf", "no soy un pdf") };
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Services.Registration.RegisterAsync(fixture.Provider.Id, fakePdf, CancellationToken.None));
    }

    [Fact]
    public async Task Internal_user_without_order_also_goes_through_approval()
    {
        await using var fixture = await Fixture.CreateAsync();

        await Assert.ThrowsAsync<ValidationException>(() => fixture.Services.Registration.RegisterAsync(
            fixture.Internal.Id, Command(DocumentEntryType.WithoutPurchaseOrder, Xml("F001-00000106", issuerRuc: "20111111111")), CancellationToken.None));
        var result = await fixture.Services.Registration.RegisterAsync(
            fixture.Internal.Id, Command(DocumentEntryType.WithoutPurchaseOrder, Xml("F001-00000106", issuerRuc: "20111111111"), approverId: fixture.Approver.Id), CancellationToken.None);

        Assert.Equal(DocumentStatus.PendingApproval, result.Status);
        Assert.False(result.IsPettyCash);
        Assert.Equal("María Torres", result.ApproverName);
    }

    [Fact]
    public async Task Petty_cash_skips_approval_but_only_for_internal_users_and_without_order()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Services.Registration.RegisterAsync(
            fixture.Internal.Id, Command(DocumentEntryType.WithoutPurchaseOrder, Xml("F001-00000112", issuerRuc: "20111111111"), pettyCash: true), CancellationToken.None);
        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Services.Registration.RegisterAsync(
            fixture.Provider.Id, Command(DocumentEntryType.WithoutPurchaseOrder, Xml("F001-00000113"), approverId: fixture.Approver.Id, pettyCash: true), CancellationToken.None));
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Services.Registration.RegisterAsync(
            fixture.Internal.Id, Command(DocumentEntryType.WithPurchaseOrder, Xml("F001-00000114", issuerRuc: "20111111111"), orderNumber: "4500012873", pettyCash: true), CancellationToken.None));

        Assert.Equal(DocumentStatus.PendingAccounting, result.Status);
        Assert.True(result.IsPettyCash);
        Assert.Null(result.ApproverName);
        Assert.Empty(fixture.Email.Recipients);
    }

    [Fact]
    public async Task Internal_users_can_register_special_documents_and_providers_cannot()
    {
        await using var fixture = await Fixture.CreateAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Services.Registration.RegisterSpecialAsync(fixture.Provider.Id, Special("075-1"), CancellationToken.None));
        var special = await fixture.Services.Registration.RegisterSpecialAsync(fixture.Internal.Id, Special("075-1"), CancellationToken.None);

        Assert.Equal("Boleto aéreo", special.DocumentType);
        Assert.Equal(DocumentStatus.PendingAccounting, special.Status);
    }

    [Fact]
    public async Task Extra_pdfs_are_consolidated_into_a_single_attachment()
    {
        await using var fixture = await Fixture.CreateAsync();
        var extras = new[] { RealPdf("acta.pdf", 2), RealPdf("fotos.pdf", 3) };

        var result = await fixture.Services.Registration.RegisterAsync(
            fixture.Provider.Id, Command(DocumentEntryType.WithPurchaseOrder, Xml("F001-00000115"), orderNumber: "4500012873", extras: extras), CancellationToken.None);

        var support = Assert.Single(result.Attachments, attachment => attachment.Kind == AttachmentKind.Support);
        Assert.Equal("Anexos_F001-00000115.pdf", support.FileName);
        var stored = fixture.Storage.Files.Single(file => file.Value.AsSpan().StartsWith("%PDF"u8) && file.Value.Length > 200 && file.Key.EndsWith(".pdf") && IsMergedOf(file.Value, 5));
        Assert.NotNull(stored.Key);
    }

    [Fact]
    public async Task Unreadable_extra_pdf_rejects_the_registration_without_leaving_files()
    {
        await using var fixture = await Fixture.CreateAsync();
        var broken = File("roto.pdf", "%PDF-1.4 esto no es un pdf completo");

        await Assert.ThrowsAsync<ValidationException>(() => fixture.Services.Registration.RegisterAsync(
            fixture.Provider.Id, Command(DocumentEntryType.WithoutPurchaseOrder, Xml("F001-00000116"), approverId: fixture.Approver.Id, extras: [broken]), CancellationToken.None));

        Assert.Empty(fixture.Storage.Files);
    }

    private static bool IsMergedOf(byte[] pdf, int pages)
    {
        try
        {
            using var document = PdfSharp.Pdf.IO.PdfReader.Open(new MemoryStream(pdf), PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
            return document.PageCount == pages;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task Only_the_assigned_approver_can_approve()
    {
        await using var fixture = await Fixture.CreateAsync();
        var document = await fixture.Services.Registration.RegisterAsync(fixture.Provider.Id, Command(DocumentEntryType.WithoutPurchaseOrder, Xml("F001-00000107"), approverId: fixture.Approver.Id), CancellationToken.None);
        var request = new ApproveDocumentRequest { ReferenceType = ApprovalReferenceType.Order, Reference = "ped-2026-01842" };

        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Services.Approvals.ApproveAsync(fixture.OtherApprover.Id, document.Id, request, CancellationToken.None));
        var approved = await fixture.Services.Approvals.ApproveAsync(fixture.Approver.Id, document.Id, request, CancellationToken.None);

        Assert.Equal(DocumentStatus.PendingAccounting, approved.Status);
        Assert.Equal("PED-2026-01842", approved.ApprovalReference);
        Assert.Equal(DocumentEventKind.Current, approved.History[^1].Kind);
        Assert.DoesNotContain(approved.History.SkipLast(1), item => item.Kind == DocumentEventKind.Current);
    }

    [Fact]
    public async Task Reassign_moves_the_document_and_notifies_the_new_approver()
    {
        await using var fixture = await Fixture.CreateAsync();
        var document = await fixture.Services.Registration.RegisterAsync(fixture.Provider.Id, Command(DocumentEntryType.WithoutPurchaseOrder, Xml("F001-00000108"), approverId: fixture.Approver.Id), CancellationToken.None);

        var reassigned = await fixture.Services.Approvals.ReassignAsync(fixture.Approver.Id, document.Id, new ReassignDocumentRequest { ApproverId = fixture.OtherApprover.Id, Reason = "Corresponde a Finanzas" }, CancellationToken.None);

        Assert.Equal("Jorge Paredes", reassigned.ApproverName);
        Assert.Equal("Corresponde a Finanzas", reassigned.History.Single(item => item.Title.StartsWith("Reasignado")).Note);
        Assert.Equal("jparedes@test.pe", fixture.Email.Recipients[^1]);
        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Services.Approvals.RejectAsync(
            fixture.Approver.Id, document.Id, new RejectDocumentRequest { Reason = "x" }, CancellationToken.None));
    }

    [Fact]
    public async Task Reassign_requires_a_reason()
    {
        await using var fixture = await Fixture.CreateAsync();
        var document = await fixture.Services.Registration.RegisterAsync(fixture.Provider.Id, Command(DocumentEntryType.WithoutPurchaseOrder, Xml("F001-00000117"), approverId: fixture.Approver.Id), CancellationToken.None);

        await Assert.ThrowsAsync<DomainRuleException>(() => fixture.Services.Approvals.ReassignAsync(
            fixture.Approver.Id, document.Id, new ReassignDocumentRequest { ApproverId = fixture.OtherApprover.Id, Reason = "  " }, CancellationToken.None));
    }

    [Fact]
    public async Task Accounting_can_observe_pending_documents_once()
    {
        await using var fixture = await Fixture.CreateAsync();
        var document = await fixture.Services.Registration.RegisterAsync(fixture.Provider.Id, Command(DocumentEntryType.WithPurchaseOrder, Xml("F001-00000109"), orderNumber: "4500012873"), CancellationToken.None);
        var request = new ObserveDocumentRequest { Reason = "Falta la guía de remisión.", Email = "Proveedor@Test.pe" };

        var observed = await fixture.Services.Accounting.ObserveAsync(fixture.Accounting.Id, document.Id, request, CancellationToken.None);

        Assert.Equal(DocumentStatus.Observed, observed.Status);
        Assert.Equal("proveedor@test.pe", fixture.Email.Recipients[^1]);
        await Assert.ThrowsAsync<DomainRuleException>(() => fixture.Services.Accounting.ObserveAsync(fixture.Accounting.Id, document.Id, request, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Services.Accounting.ObserveAsync(fixture.Provider.Id, document.Id, request, CancellationToken.None));
    }

    [Fact]
    public async Task Inboxes_are_scoped_by_role()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Services.Registration.RegisterAsync(fixture.Provider.Id, Command(DocumentEntryType.WithoutPurchaseOrder, Xml("F001-00000110"), approverId: fixture.Approver.Id), CancellationToken.None);
        await fixture.Services.Registration.RegisterAsync(fixture.Provider.Id, Command(DocumentEntryType.WithPurchaseOrder, Xml("F001-00000111"), orderNumber: "4500012873"), CancellationToken.None);

        var approvals = await fixture.Services.Queries.SearchAsync(fixture.Approver.Id, DocumentInbox.Approvals, null, null, 1, 10, CancellationToken.None);
        var accounting = await fixture.Services.Queries.SearchAsync(fixture.Accounting.Id, DocumentInbox.Accounting, null, null, 1, 10, CancellationToken.None);
        var mine = await fixture.Services.Queries.SearchAsync(fixture.Provider.Id, DocumentInbox.Mine, null, null, 1, 10, CancellationToken.None);

        Assert.Equal("F001-00000110", Assert.Single(approvals.Items).Number);
        Assert.Equal("F001-00000111", Assert.Single(accounting.Items).Number);
        Assert.Equal(2, mine.Total);
        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Services.Queries.SearchAsync(fixture.Provider.Id, DocumentInbox.Accounting, null, null, 1, 10, CancellationToken.None));
    }

    [Fact]
    public async Task Users_only_work_with_their_assigned_companies()
    {
        await using var fixture = await Fixture.CreateAsync();

        var internalCompanies = await fixture.Services.Catalog.ListCompaniesAsync(fixture.Internal.Id, CancellationToken.None);
        Assert.Equal("1001", Assert.Single(internalCompanies).Code);
        Assert.Equal(2, (await fixture.Services.Catalog.ListCompaniesAsync(fixture.Provider.Id, CancellationToken.None)).Count);

        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Services.Registration.RegisterAsync(
            fixture.Internal.Id, Command(DocumentEntryType.WithoutPurchaseOrder, Xml("F001-00000120", issuerRuc: "20111111111"), approverId: fixture.OtherApprover.Id, companyCode: "1002"), CancellationToken.None));
        // El aprobador elegido también debe trabajar con la sociedad del documento.
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Services.Registration.RegisterAsync(
            fixture.Provider.Id, Command(DocumentEntryType.WithoutPurchaseOrder, Xml("F001-00000121"), approverId: fixture.Approver.Id, companyCode: "1002"), CancellationToken.None));

        var petrolera = await fixture.Services.Registration.RegisterAsync(
            fixture.Provider.Id, Command(DocumentEntryType.WithPurchaseOrder, Xml("F001-00000122"), orderNumber: "4500012873", companyCode: "1002"), CancellationToken.None);

        // Cuentas por pagar solo trabaja con Naviera: no ve ni puede observar el documento de Petrolera.
        var accounting = await fixture.Services.Queries.SearchAsync(fixture.Accounting.Id, DocumentInbox.Accounting, null, null, 1, 10, CancellationToken.None);
        Assert.Empty(accounting.Items);
        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Services.Queries.GetAsync(fixture.Accounting.Id, petrolera.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Services.Accounting.ObserveAsync(
            fixture.Accounting.Id, petrolera.Id, new ObserveDocumentRequest { Reason = "Falta la guía de remisión", Email = "proveedor@test.pe" }, CancellationToken.None));
    }

    [Fact]
    public void UblDocumentReader_rejects_dtd_and_reads_totals()
    {
        const string xxe = """<?xml version="1.0"?><!DOCTYPE Invoice [<!ENTITY x SYSTEM "file:///etc/passwd">]><Invoice><ID>&x;</ID></Invoice>""";

        Assert.Null(UblDocumentReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(xxe))));
        var document = UblDocumentReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(Xml("E001-00000001"))));
        Assert.NotNull(document);
        Assert.False(document.RequiresCdr);
        Assert.Equal(ProviderRuc, document.IssuerRuc);
        Assert.Equal(1180m, document.Total);
    }

    // ——— Datos de prueba ———

    private static RegisterElectronicDocumentCommand Command(
        DocumentEntryType entryType, string xml, Guid? approverId = null, string? orderNumber = null, bool includeCdr = true,
        bool pettyCash = false, IReadOnlyList<UploadedFile>? extras = null, string companyCode = "1001")
    {
        var number = xml.Split("<cbc:ID>")[1].Split('<')[0];
        return new RegisterElectronicDocumentCommand(
            entryType, companyCode, pettyCash, orderNumber is null ? null : OrderType.Service, orderNumber, approverId,
            File($"{number}.xml", xml), File($"{number}.pdf", "%PDF-1.4 prueba"),
            includeCdr ? File($"R-{number}.zip", "PK prueba") : null,
            extras ?? []);
    }

    private static UploadedFile RealPdf(string name, int pages)
    {
        using var document = new PdfDocument();
        for (var i = 0; i < pages; i++) document.AddPage();
        using var buffer = new MemoryStream();
        document.Save(buffer, closeStream: false);
        return new UploadedFile(name, "application/pdf", buffer.Length, new MemoryStream(buffer.ToArray()));
    }

    private static RegisterSpecialDocumentCommand Special(string number) =>
        new("1001", SpecialDocumentType.AirTicket, "20341841357", new DateOnly(2026, 9, 25), number, 1236.50m, Currency.USD, File($"{number}.pdf", "%PDF-1.4 boleto"));

    private static UploadedFile File(string name, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new UploadedFile(name, "application/octet-stream", bytes.Length, new MemoryStream(bytes));
    }

    private static string Xml(string number, string issuerRuc = ProviderRuc) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
          xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"
          xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">
          <cbc:ID>{number}</cbc:ID>
          <cbc:IssueDate>2026-10-01</cbc:IssueDate>
          <cbc:InvoiceTypeCode>01</cbc:InvoiceTypeCode>
          <cbc:DocumentCurrencyCode>PEN</cbc:DocumentCurrencyCode>
          <cac:AccountingSupplierParty><cac:Party>
            <cac:PartyIdentification><cbc:ID>{issuerRuc}</cbc:ID></cac:PartyIdentification>
            <cac:PartyLegalEntity><cbc:RegistrationName>ANDES S.A.C.</cbc:RegistrationName></cac:PartyLegalEntity>
          </cac:Party></cac:AccountingSupplierParty>
          <cac:AccountingCustomerParty><cac:Party>
            <cac:PartyIdentification><cbc:ID>{CompanyRuc}</cbc:ID></cac:PartyIdentification>
          </cac:Party></cac:AccountingCustomerParty>
          <cac:TaxTotal><cbc:TaxAmount>180.00</cbc:TaxAmount></cac:TaxTotal>
          <cac:LegalMonetaryTotal>
            <cbc:LineExtensionAmount>1000.00</cbc:LineExtensionAmount>
            <cbc:PayableAmount>1180.00</cbc:PayableAmount>
          </cac:LegalMonetaryTotal>
          <cac:InvoiceLine>
            <cbc:InvoicedQuantity>1</cbc:InvoicedQuantity>
            <cbc:LineExtensionAmount>1000.00</cbc:LineExtensionAmount>
            <cac:Item><cbc:Description>Servicio de mantenimiento</cbc:Description></cac:Item>
            <cac:Price><cbc:PriceAmount>1000.00</cbc:PriceAmount></cac:Price>
          </cac:InvoiceLine>
        </Invoice>
        """.Trim();

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(AppDbContext db) => Db = db;

        public AppDbContext Db { get; }
        public FakeEmailSender Email { get; } = new();
        public MemoryStorage Storage { get; } = new();
        public DocumentServices Services { get; private set; } = null!;
        public AppUser Provider { get; private set; } = null!;
        public AppUser Internal { get; private set; } = null!;
        public AppUser Approver { get; private set; } = null!;
        public AppUser OtherApprover { get; private set; } = null!;
        public AppUser Accounting { get; private set; } = null!;

        public static async Task<Fixture> CreateAsync()
        {
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"documents-{Guid.NewGuid():N}").Options);
            var fixture = new Fixture(db);
            var roles = SecurityCatalog.Roles.ToDictionary(role => role.Key, role => new Role { Code = role.Key, Name = role.Value });
            db.Roles.AddRange(roles.Values);
            TestMenus.Seed(db, roles);
            var naviera = new Company { Code = "1001", Name = "Naviera Transoceánica", Ruc = CompanyRuc };
            var finance = new Area { Code = "FINANZAS", Name = "Finanzas", Company = naviera, CompanyId = naviera.Id };
            db.Areas.Add(finance);
            // Petrolera sin RUC: el receptor del XML no se valida para ella.
            var petrolera = new Company { Code = "1002", Name = "Petrolera Transoceánica" };
            db.Companies.AddRange(naviera, petrolera);

            AppUser User(string name, string email, string role, Area? area = null, string? ruc = null, params Company[] companies)
            {
                var user = AppUser.Create(email, name, ruc, email, "x", DateTime.UtcNow);
                user.AssignArea(area);
                user.SetRoles([roles[role]]);
                user.SetCompanies(companies.Length > 0 ? companies : [naviera]);
                db.Users.Add(user);
                return user;
            }

            // Solo el proveedor y el segundo aprobador trabajan también con Petrolera.
            fixture.Provider = User("Andes Suministros", "proveedor@test.pe", SecurityCatalog.ProviderRole, ruc: ProviderRuc, companies: [naviera, petrolera]);
            fixture.Internal = User("Rocío Medina", "rmedina@test.pe", SecurityCatalog.InternalUserRole);
            fixture.Approver = User("María Torres", "mtorres@test.pe", SecurityCatalog.AreaApproverRole, finance);
            fixture.OtherApprover = User("Jorge Paredes", "jparedes@test.pe", SecurityCatalog.AreaApproverRole, finance, companies: [naviera, petrolera]);
            fixture.Accounting = User("Cuentas por pagar", "cxp@test.pe", SecurityCatalog.AccountsPayableRole);
            await db.SaveChangesAsync();

            fixture.Services = TestServices.Documents(db, fixture.Storage, fixture.Email);
            return fixture;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<string> Recipients { get; } = [];

        public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken, bool isHtml = false, IReadOnlyList<string>? copyTo = null)
        {
            Recipients.Add(recipient);
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryStorage : IFileStorage
    {
        public Dictionary<string, byte[]> Files { get; } = [];

        public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            var key = $"{Guid.NewGuid():N}{extension}";
            Files[key] = buffer.ToArray();
            return key;
        }

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream(Files[storageKey]));

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
        {
            Files.Remove(storageKey);
            return Task.CompletedTask;
        }
    }
}
