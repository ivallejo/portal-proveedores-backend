using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Abstractions.Documents;
using WebProveedores.Application.Abstractions.Auth;
using WebProveedores.Application.Admin;
using WebProveedores.Application.Abstractions.Persistence;
using WebProveedores.Application.Documents;
using WebProveedores.Application.Profile;
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

        var now = DateTime.UtcNow;
        var byEmail = await repository.SearchAsync(new UserSearchFilter(data.Approver.PrimaryEmail, SecurityCatalog.AreaApproverRole, UserStatus.Active, now), 1, 10, CancellationToken.None);
        var firstPage = await repository.SearchAsync(new UserSearchFilter(data.Ruc, null, null, now), 1, 1, CancellationToken.None);
        var inactive = await repository.SearchAsync(new UserSearchFilter(data.Ruc, null, UserStatus.Inactive, now), 1, 10, CancellationToken.None);
        var (total, active) = await repository.CountByStatusAsync(now, CancellationToken.None);

        Assert.Empty(inactive.Items);
        Assert.True(total >= active && active > 0);

        Assert.Equal(data.Approver.Id, Assert.Single(byEmail.Items).Id);
        Assert.Equal([data.Naviera.Code], byEmail.Items[0].UserCompanies.Select(item => item.Company.Code));
        Assert.Single(firstPage.Items);
        Assert.Equal(1, firstPage.Total);
    }

    [Fact]
    public async Task Profile_email_is_verified_and_made_primary_in_sql()
    {
        var data = await SeedAsync();
        var email = new CapturingEmail();
        var address = $"alterno-{Guid.NewGuid():N}@ejemplo.test";
        await using (var db = sql.CreateContext())
        {
            var service = new ProfileService(new EfUserRepository(db), new EfUnitOfWork(db), new EmailVerifications(email, TestServices.Portal, TimeProvider.System), TimeProvider.System);
            await service.AddEmailAsync(data.Approver.Id, new AddEmailRequest { Email = address, Type = "personal" }, CancellationToken.None);
        }
        var token = System.Text.RegularExpressions.Regex.Match(email.Body, "emailToken=([A-Za-z0-9_-]+)").Groups[1].Value;
        await using (var db = sql.CreateContext())
        {
            var service = new ProfileService(new EfUserRepository(db), new EfUnitOfWork(db), new EmailVerifications(email, TestServices.Portal, TimeProvider.System), TimeProvider.System);
            Assert.Equal(address, await service.VerifyEmailAsync(token, CancellationToken.None));
        }
        await using (var db = sql.CreateContext())
        {
            var service = new ProfileService(new EfUserRepository(db), new EfUnitOfWork(db), new EmailVerifications(email, TestServices.Portal, TimeProvider.System), TimeProvider.System);
            var added = (await service.GetAsync(data.Approver.Id, CancellationToken.None)).Emails.Single(item => item.Email == address);
            // El índice único de «un principal por usuario» exige quitar el anterior antes de marcar el nuevo.
            var profile = await service.MakePrimaryAsync(data.Approver.Id, added.Id, CancellationToken.None);
            Assert.Equal(address, profile.Emails.Single(item => item.IsPrimary).Email);
        }
        await using (var db = sql.CreateContext())
        {
            var user = await new EfUserRepository(db).FindForLoginAsync(address, address, CancellationToken.None);
            Assert.Equal(data.Approver.Id, user?.Id);
        }
    }

    [Fact]
    public async Task Admin_creates_a_user_and_switches_its_primary_email_in_sql()
    {
        var data = await SeedAsync();
        var dni = Random.Shared.Next(10_000_000, 99_999_999).ToString();
        var first = $"primero-{Guid.NewGuid():N}@ejemplo.test";
        var second = $"segundo-{Guid.NewGuid():N}@ejemplo.test";
        Guid id;
        await using (var db = sql.CreateContext())
        {
            var created = await TestServices.Admin(db, new CapturingEmail()).CreateAsync(new SaveUserRequest
            {
                Role = SecurityCatalog.AreaApproverRole,
                Document = dni,
                FirstName = "Ana",
                LastName = "Ríos",
                AreaId = data.Area.Id,
                CompanyCodes = [data.Naviera.Code],
                Emails = [new UserEmailInput { Email = first, IsPrimary = true }],
            }, CancellationToken.None);
            id = created.Id;
        }
        await using (var db = sql.CreateContext())
        {
            var admin = TestServices.Admin(db, new CapturingEmail());
            var detail = await admin.GetAsync(id, CancellationToken.None);
            // Cuenta aún no activada: el nuevo correo puede ser principal (la activación lo verificará).
            var updated = await admin.UpdateAsync(data.Accounting.Id, id, new SaveUserRequest
            {
                Role = SecurityCatalog.AreaApproverRole,
                FirstName = "Ana",
                LastName = "Ríos Campos",
                AreaId = data.Area.Id,
                CompanyCodes = [data.Naviera.Code],
                Emails = [new UserEmailInput { Id = detail!.Emails[0].Id, Email = first }, new UserEmailInput { Email = second, IsPrimary = true }],
            }, CancellationToken.None);
            Assert.Equal(second, updated!.Emails.Single(email => email.IsPrimary).Email);
        }
        await using (var db = sql.CreateContext())
        {
            var page = await TestServices.Admin(db, new CapturingEmail()).SearchAsync(dni, SecurityCatalog.AreaApproverRole, "active", 1, 10, CancellationToken.None);
            Assert.Equal(("Ana Ríos Campos", second, "DNI"), (page.Items.Single().DisplayName, page.Items.Single().PrimaryEmail, page.Items.Single().DocumentType));
        }
    }

    [Fact]
    public async Task Permissions_and_roles_translate_to_sql()
    {
        var data = await SeedAsync();
        await using var db = sql.CreateContext();
        var access = new EfAccessRepository(db);

        var permissions = await access.PermissionsOfAsync(data.Approver.Id, CancellationToken.None);
        var roles = await access.ListRolesAsync(CancellationToken.None);

        Assert.Contains(MenuCatalog.Documents, permissions);
        Assert.DoesNotContain(MenuCatalog.Accounting, permissions);
        Assert.True(roles.Single(item => item.Role.Code == SecurityCatalog.AreaApproverRole).UserCount > 0);
        Assert.NotEmpty((await access.ListMenusAsync(CancellationToken.None)).Single(menu => menu.Code == MenuCatalog.Home).RoleMenus);
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
                new PasswordResetToken { UserId = data.Provider.Id, TokenHash = $"VENCIDO-{data.Ruc}", Purpose = PasswordTokenPurpose.Activation, ExpiresAtUtc = DateTime.UtcNow.AddHours(-1) },
                new PasswordResetToken { UserId = data.Provider.Id, TokenHash = $"REEMPLAZADO-{data.Ruc}", Purpose = PasswordTokenPurpose.Activation, ExpiresAtUtc = DateTime.UtcNow.AddHours(1), RevokedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        await using var read = sql.CreateContext();
        var tokens = new EfPasswordTokenRepository(read, TimeProvider.System);

        Assert.NotNull(await tokens.FindValidAsync($"VIGENTE-{data.Ruc}", PasswordTokenPurpose.Activation, CancellationToken.None));
        Assert.Null(await tokens.FindValidAsync($"VIGENTE-{data.Ruc}", PasswordTokenPurpose.PasswordReset, CancellationToken.None));
        Assert.Null(await tokens.FindValidAsync($"VENCIDO-{data.Ruc}", PasswordTokenPurpose.Activation, CancellationToken.None));
        Assert.Null(await tokens.FindValidAsync($"REEMPLAZADO-{data.Ruc}", PasswordTokenPurpose.Activation, CancellationToken.None));
        Assert.Equal(3, (await tokens.ListForUserAsync(data.Provider.Id, CancellationToken.None)).Count);
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
        if (!await db.MenuOptions.AnyAsync()) TestMenus.Seed(db, roles);

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

    private sealed class CapturingEmail : IEmailSender
    {
        public string Body { get; private set; } = string.Empty;

        public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken, bool isHtml = false, IReadOnlyList<string>? copyTo = null)
        {
            Body = body;
            return Task.CompletedTask;
        }
    }
}
