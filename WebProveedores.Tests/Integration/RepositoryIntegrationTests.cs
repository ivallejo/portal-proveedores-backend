using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Abstractions.Documents;
using WebProveedores.Application.Documents;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Entities;
using WebProveedores.Infrastructure.Persistence;

namespace WebProveedores.Tests.Integration;

/// <summary>Repositorios contra SQL Server real. Cada prueba crea sus propios datos con valores únicos.</summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class RepositoryIntegrationTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Approvers_query_with_company_codes_translates_to_sql()
    {
        var data = await SeedAsync();
        await using var db = sql.CreateContext();
        var repository = new EfDocumentRepository(db);

        var approver = await repository.FindApproverAsync(data.Approver.Id, CancellationToken.None);
        var all = await repository.ListApproversAsync(CancellationToken.None);

        Assert.NotNull(approver);
        Assert.Equal(data.Area.Name, approver.AreaName);
        Assert.Equal([data.Naviera.Code], approver.CompanyCodes);
        Assert.Contains(all, item => item.UserId == data.Approver.Id);
        Assert.DoesNotContain(all, item => item.UserId == data.Accounting.Id);
    }

    [Fact]
    public async Task Inbox_search_filters_by_company_and_counts_by_status()
    {
        var data = await SeedAsync();
        await AddDocumentAsync(data, data.Naviera, "F001-1");
        await AddDocumentAsync(data, data.Ultratag, "F001-2");
        await using var db = sql.CreateContext();
        var repository = new EfDocumentRepository(db);

        var query = new DocumentQuery(DocumentInbox.Accounting, data.Ruc, null, 1, 10, CompanyIds: [data.Naviera.Id]);
        var page = await repository.SearchAsync(query, CancellationToken.None);

        Assert.Equal("F001-1", Assert.Single(page.Items).Number);
        Assert.Equal(1, page.CountsByStatus[DocumentStatus.PendingAccounting]);
    }

    [Fact]
    public async Task Duplicate_document_is_rejected_by_the_unique_index()
    {
        var data = await SeedAsync();
        await AddDocumentAsync(data, data.Naviera, "F001-9");

        var exception = await Assert.ThrowsAsync<DocumentRejectedException>(() => AddDocumentAsync(data, data.Naviera, "F001-9"));
        Assert.Contains("ya fue registrado", exception.Message);
    }

    [Fact]
    public async Task User_search_matches_emails_and_paginates()
    {
        var data = await SeedAsync();
        await using var db = sql.CreateContext();
        var repository = new EfUserRepository(db);

        var byEmail = await repository.SearchAsync(data.Approver.PrimaryEmail, 1, 10, CancellationToken.None);
        var firstPage = await repository.SearchAsync(data.Ruc, 1, 1, CancellationToken.None);

        Assert.Equal(data.Approver.Id, Assert.Single(byEmail.Items).Id);
        Assert.Equal([data.Naviera.Code], byEmail.Items[0].UserCompanies.Select(item => item.Company.Code));
        Assert.Single(firstPage.Items);
        Assert.Equal(1, firstPage.Total);
    }

    [Fact]
    public async Task Organization_lists_count_areas_and_users_in_sql()
    {
        var data = await SeedAsync();
        await using var db = sql.CreateContext();
        var repository = new EfOrganizationRepository(db);

        var naviera = (await repository.ListCompaniesAsync(CancellationToken.None)).Single(item => item.Company.Id == data.Naviera.Id);
        var area = (await repository.ListAreasAsync(CancellationToken.None)).Single(item => item.Area.Id == data.Area.Id);

        Assert.Equal((1, 3), (naviera.AreaCount, naviera.UserCount));
        Assert.Equal((data.Naviera.Code, 1), (area.Area.Company.Code, area.UserCount));
        Assert.True(await repository.AreaCodeExistsAsync(data.Naviera.Id, data.Area.Code, null, CancellationToken.None));
        Assert.False(await repository.AreaCodeExistsAsync(data.Ultratag.Id, data.Area.Code, null, CancellationToken.None));
    }

    [Fact]
    public async Task Password_token_is_found_only_while_valid_and_unused()
    {
        var data = await SeedAsync();
        await using (var db = sql.CreateContext())
        {
            db.PasswordResetTokens.AddRange(
                new PasswordResetToken { UserId = data.Provider.Id, TokenHash = $"VIGENTE-{data.Ruc}", Purpose = PasswordTokenPurpose.Activation, ExpiresAtUtc = DateTime.UtcNow.AddHours(1) },
                new PasswordResetToken { UserId = data.Provider.Id, TokenHash = $"VENCIDO-{data.Ruc}", Purpose = PasswordTokenPurpose.Activation, ExpiresAtUtc = DateTime.UtcNow.AddHours(-1) });
            await db.SaveChangesAsync();
        }
        await using var read = sql.CreateContext();
        var tokens = new EfPasswordTokenRepository(read, TimeProvider.System);

        Assert.NotNull(await tokens.FindValidAsync(data.Ruc, $"VIGENTE-{data.Ruc}", PasswordTokenPurpose.Activation, CancellationToken.None));
        Assert.Null(await tokens.FindValidAsync(data.Ruc, $"VIGENTE-{data.Ruc}", PasswordTokenPurpose.PasswordReset, CancellationToken.None));
        Assert.Null(await tokens.FindValidAsync(data.Ruc, $"VENCIDO-{data.Ruc}", PasswordTokenPurpose.Activation, CancellationToken.None));
    }

    private async Task AddDocumentAsync(SeedData data, Company company, string number)
    {
        await using var db = sql.CreateContext();
        var repository = new EfDocumentRepository(db);
        var tracked = await db.Companies.SingleAsync(item => item.Id == company.Id);
        var document = SupplierDocument.Register(
            new DocumentData(number, DocumentEntryType.WithPurchaseOrder, "Factura", data.Ruc, "Proveedor de prueba", null, Currency.PEN, 100m, 18m, 118m, "Servicio", new DateOnly(2026, 10, 1), "Validado"),
            tracked, data.Provider.Id, "Proveedor (proveedor)", DateTime.UtcNow, "Validado en SAP",
            order: new PurchaseOrderInfo(OrderType.Service, "4500012873", 18450m, "Mantenimiento"));
        document.AddItem("Servicio", 1, 100m, 100m);
        repository.Add(document);
        await repository.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Datos propios de cada prueba: sociedades, área, roles y usuarios con un RUC único.</summary>
    private async Task<SeedData> SeedAsync()
    {
        var suffix = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(4));
        var ruc = $"20{Random.Shared.NextInt64(100_000_000, 999_999_999)}";
        await using var db = sql.CreateContext();

        var roles = await db.Roles.ToDictionaryAsync(role => role.Code);
        foreach (var (code, name) in SecurityCatalog.Roles.Where(role => !roles.ContainsKey(role.Key)))
            db.Roles.Add(roles[code] = new Role { Code = code, Name = name });

        var naviera = new Company { Code = $"N{suffix[..6]}", Name = $"Naviera {suffix}" };
        var ultratag = new Company { Code = $"U{suffix[..6]}", Name = $"Ultratag {suffix}" };
        var area = new Area { Code = $"AREA_{suffix}", Name = $"Área {suffix}", Company = naviera, CompanyId = naviera.Id };
        db.Companies.AddRange(naviera, ultratag);
        db.Areas.Add(area);

        AppUser User(string name, string role, string? userRuc = null, Area? userArea = null, params Company[] companies)
        {
            var user = AppUser.Create($"{name}.{suffix}", $"{name} {suffix}", userRuc, $"{name}.{suffix}@ejemplo.test", "x", DateTime.UtcNow);
            user.AssignArea(userArea);
            user.SetRoles([roles[role]]);
            user.SetCompanies(companies);
            db.Users.Add(user);
            return user;
        }

        var provider = User("proveedor", SecurityCatalog.ProviderRole, ruc, null, naviera, ultratag);
        var approver = User("aprobador", SecurityCatalog.AreaApproverRole, null, area, naviera);
        var accounting = User("cxp", SecurityCatalog.AccountsPayableRole, null, null, naviera);
        await db.SaveChangesAsync();
        return new SeedData(ruc, naviera, ultratag, area, provider, approver, accounting);
    }

    private sealed record SeedData(string Ruc, Company Naviera, Company Ultratag, Area Area, AppUser Provider, AppUser Approver, AppUser Accounting);
}
