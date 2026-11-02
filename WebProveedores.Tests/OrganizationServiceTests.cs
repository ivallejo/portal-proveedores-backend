using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Application.Organization;
using WebProveedores.Application.Organization.Commands;
using WebProveedores.Domain.Identity;
using WebProveedores.Infrastructure.Persistence;
using WebProveedores.Infrastructure.Persistence.Repositories;

namespace WebProveedores.Tests;

public sealed class OrganizationServiceTests
{
    [Fact]
    public async Task Company_rules_follow_the_prototype()
    {
        await using var db = CreateContext();
        var service = Service(db);
        var created = await service.CreateCompanyAsync(Company("ntr", "20522163890", "Facturacion@Navitranso.com"), CancellationToken.None);

        Assert.Equal(("NTR", "facturacion@navitranso.com", true), (created.Code, created.BillingEmail, created.IsActive));
        await Assert.ThrowsAsync<ConflictException>(() => service.CreateCompanyAsync(Company("NTR", "20100126606"), CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() => service.CreateCompanyAsync(Company("PTR", "20522163890"), CancellationToken.None));
        var personRuc = await Assert.ThrowsAsync<ValidationException>(() => service.CreateCompanyAsync(Company("PTR", "10456789012"), CancellationToken.None));
        Assert.Contains("empieza con 20", personRuc.Message);
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateCompanyAsync(Company("CODIGO", "20100126606"), CancellationToken.None));
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateCompanyAsync(Company("PTR", "20100126606", "sin-arroba"), CancellationToken.None));

        var inactive = await service.SetCompanyStatusAsync(created.Id, false, CancellationToken.None);
        Assert.False(inactive.IsActive);
        Assert.Single(await service.ListCompaniesAsync("ntr", null, CancellationToken.None));
        Assert.Empty(await service.ListCompaniesAsync(null, true, CancellationToken.None));
    }

    [Fact]
    public async Task Area_belongs_to_several_companies_and_its_name_is_unique()
    {
        await using var db = CreateContext();
        var service = Service(db);
        var naviera = await service.CreateCompanyAsync(Company("1001", "20522163890"), CancellationToken.None);
        var petral = await service.CreateCompanyAsync(Company("1003", "20511922578"), CancellationToken.None);

        var finanzas = await service.CreateAreaAsync(Area("Finanzas", "Tesorería", naviera.Id, petral.Id), CancellationToken.None);
        Assert.Equal(["1001", "1003"], finanzas.Companies.Select(company => company.Code));
        // El nombre es único en todo el portal (ya no por sociedad).
        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAreaAsync(Area("finanzas", null, petral.Id), CancellationToken.None));
        var none = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAreaAsync(Area("Caja", null), CancellationToken.None));
        Assert.Contains("al menos una sociedad", none.Message);

        // Una sociedad inactiva no se agrega, pero el área que ya la tiene la conserva.
        await service.SetCompanyStatusAsync(petral.Id, false, CancellationToken.None);
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAreaAsync(Area("Logística", null, petral.Id), CancellationToken.None));
        var renamed = await service.UpdateAreaAsync(finanzas.Id, Area("Tesorería", "  ", naviera.Id, petral.Id), CancellationToken.None);
        Assert.Equal(("Tesorería", null, 2), (renamed.Name, renamed.Description, renamed.Companies.Count));

        await service.CreateAreaAsync(Area("Compras", null, naviera.Id), CancellationToken.None);
        Assert.Equal(2, (await service.ListAreasAsync(null, null, naviera.Id, CancellationToken.None)).Count);
        Assert.Single(await service.ListAreasAsync(null, null, petral.Id, CancellationToken.None));
        Assert.Single(await service.ListAreasAsync("1003", null, null, CancellationToken.None));
    }

    [Fact]
    public async Task A_company_is_not_removed_while_area_users_depend_on_it()
    {
        await using var db = CreateContext();
        var service = Service(db);
        var naviera = await service.CreateCompanyAsync(Company("1001", "20522163890"), CancellationToken.None);
        var petral = await service.CreateCompanyAsync(Company("1003", "20511922578"), CancellationToken.None);
        var area = await service.CreateAreaAsync(Area("Finanzas", null, naviera.Id, petral.Id), CancellationToken.None);
        var user = AppUser.Create("ana.rios", "Ana Ríos", null, "ana@ejemplo.test", "x", DateTime.UtcNow);
        user.AssignArea(await db.Areas.SingleAsync(item => item.Id == area.Id));
        user.SetCompanies([await db.Companies.SingleAsync(item => item.Id == petral.Id)]);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Ana solo tiene Petral: quitar Petral la dejaría sin sociedades en común con el área.
        var blocked = await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAreaAsync(area.Id, Area("Finanzas", null, naviera.Id), CancellationToken.None));
        Assert.Contains("Ana Ríos", blocked.Message);
        var updated = await service.UpdateAreaAsync(area.Id, Area("Finanzas", null, petral.Id), CancellationToken.None);
        Assert.Equal(["1003"], updated.Companies.Select(company => company.Code));
    }

    private static SaveCompanyCommand Company(string code, string ruc, string email = "facturacion@ejemplo.test") =>
        new() { Code = code, Name = $"Sociedad {code}", Ruc = ruc, BillingEmail = email };

    private static SaveAreaCommand Area(string name, string? description, params Guid[] companyIds) =>
        new() { CompanyIds = companyIds, Name = name, Description = description };

    private static OrganizationService Service(AppDbContext db) => new(new EfOrganizationReader(db), new EfOrganizationRepository(db), new EfUnitOfWork(db));

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"organization-{Guid.NewGuid():N}").Options);
}
