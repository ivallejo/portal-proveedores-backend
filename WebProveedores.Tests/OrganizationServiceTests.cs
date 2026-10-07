using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Application.Organization;
using WebProveedores.Application.UseCases.Organization;
using WebProveedores.Infrastructure.Persistence;

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
    public async Task Area_name_is_unique_within_its_company_and_needs_an_active_company()
    {
        await using var db = CreateContext();
        var service = Service(db);
        var naviera = await service.CreateCompanyAsync(Company("1001", "20522163890"), CancellationToken.None);
        var petral = await service.CreateCompanyAsync(Company("1003", "20511922578"), CancellationToken.None);

        var finanzas = await service.CreateAreaAsync(Area(naviera.Id, "Finanzas", "Tesorería"), CancellationToken.None);
        // Otra sociedad puede tener un área con el mismo nombre.
        await service.CreateAreaAsync(Area(petral.Id, "Finanzas"), CancellationToken.None);
        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAreaAsync(Area(naviera.Id, "finanzas"), CancellationToken.None));

        await service.SetCompanyStatusAsync(petral.Id, false, CancellationToken.None);
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAreaAsync(Area(petral.Id, "Logística"), CancellationToken.None));
        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateAreaAsync(finanzas.Id, Area(petral.Id, "Caja"), CancellationToken.None));

        var renamed = await service.UpdateAreaAsync(finanzas.Id, Area(naviera.Id, "Tesorería", "  "), CancellationToken.None);
        Assert.Equal(("Tesorería", null, "1001"), (renamed.Name, renamed.Description, renamed.CompanyCode));
        Assert.Equal(2, (await service.ListAreasAsync(null, null, null, CancellationToken.None)).Count);
        Assert.Single(await service.ListAreasAsync(null, null, naviera.Id, CancellationToken.None));
    }

    private static CompanyRequest Company(string code, string ruc, string email = "facturacion@ejemplo.test") =>
        new() { Code = code, Name = $"Sociedad {code}", Ruc = ruc, BillingEmail = email };

    private static AreaRequest Area(Guid companyId, string name, string? description = null) =>
        new() { CompanyId = companyId, Name = name, Description = description };

    private static OrganizationService Service(AppDbContext db) => new(new EfOrganizationReader(db), new EfOrganizationRepository(db), new EfUnitOfWork(db));

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"organization-{Guid.NewGuid():N}").Options);
}
