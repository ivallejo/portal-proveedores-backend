using System.Net.Mail;
using System.Text.RegularExpressions;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Application.Organization.Commands;
using WebProveedores.Application.Organization.Responses;
using WebProveedores.Application.Ports.Outbound.Persistence;
using WebProveedores.Application.Ports.Outbound.Persistence.Models;
using WebProveedores.Domain.Organization;

namespace WebProveedores.Application.Organization;

/// <summary>
/// Reglas de Configuración › Sociedades y Áreas. No se eliminan registros: se activan o desactivan
/// y el historial (documentos, usuarios) se conserva.
/// </summary>
internal sealed partial class OrganizationService(IOrganizationReader reader, IOrganizationRepository organization, IUnitOfWork unitOfWork) : IOrganizationService
{
    // ——— Sociedades ———

    public async Task<IReadOnlyList<CompanyAdminResponse>> ListCompaniesAsync(string? search, bool? active, CancellationToken cancellationToken)
    {
        var term = search?.Trim();
        return (await reader.ListCompaniesAsync(cancellationToken))
            .Where(item => active is null || item.Company.IsActive == active)
            .Where(item => string.IsNullOrEmpty(term) || Contains(item.Company.Code, term) || Contains(item.Company.Name, term) || Contains(item.Company.Ruc, term))
            .OrderBy(item => item.Company.Code)
            .Select(ToResponse)
            .ToArray();
    }

    public async Task<CompanyAdminResponse> CreateCompanyAsync(SaveCompanyCommand request, CancellationToken cancellationToken)
    {
        var data = await ValidateCompanyAsync(request, null, cancellationToken);
        var company = new Company { Code = data.Code, Name = data.Name, Ruc = data.Ruc, BillingEmail = data.Email };
        organization.AddCompany(company);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(new CompanySummary(company, 0, 0));
    }

    public async Task<CompanyAdminResponse> UpdateCompanyAsync(Guid id, SaveCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = await reader.FindCompanyAsync(id, cancellationToken) ?? throw new NotFoundException("La sociedad no existe.");
        var data = await ValidateCompanyAsync(request, id, cancellationToken);
        (company.Code, company.Name, company.Ruc, company.BillingEmail) = (data.Code, data.Name, data.Ruc, data.Email);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await CompanyResponseAsync(id, cancellationToken);
    }

    /// <summary>Desactivada, deja de aparecer en los filtros y no se registran documentos para ella; sus áreas e historial se conservan.</summary>
    public async Task<CompanyAdminResponse> SetCompanyStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var company = await reader.FindCompanyAsync(id, cancellationToken) ?? throw new NotFoundException("La sociedad no existe.");
        company.IsActive = isActive;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await CompanyResponseAsync(id, cancellationToken);
    }

    // ——— Áreas ———

    public async Task<IReadOnlyList<AreaAdminResponse>> ListAreasAsync(string? search, bool? active, Guid? companyId, CancellationToken cancellationToken)
    {
        var term = search?.Trim();
        return (await reader.ListAreasAsync(cancellationToken))
            .Where(item => active is null || item.Area.IsActive == active)
            .Where(item => companyId is null || item.Area.HasCompany(companyId.Value))
            .Where(item => string.IsNullOrEmpty(term) || Contains(item.Area.Name, term) || Contains(item.Area.Description, term)
                || item.Area.Companies.Any(company => Contains(company.Company.Name, term) || Contains(company.Company.Code, term)))
            .OrderBy(item => item.Area.Name)
            .Select(ToResponse)
            .ToArray();
    }

    public async Task<AreaAdminResponse> CreateAreaAsync(SaveAreaCommand request, CancellationToken cancellationToken)
    {
        var (companies, name, code, description) = await ValidateAreaAsync(request, null, cancellationToken);
        var area = new Area { Code = code, Name = name, Description = description };
        area.SetCompanies(companies);
        organization.AddArea(area);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(new AreaSummary(area, 0));
    }

    public async Task<AreaAdminResponse> UpdateAreaAsync(Guid id, SaveAreaCommand request, CancellationToken cancellationToken)
    {
        var area = await reader.FindAreaAsync(id, cancellationToken) ?? throw new NotFoundException("El área no existe.");
        var (companies, name, code, description) = await ValidateAreaAsync(request, area, cancellationToken);
        await EnsureCompaniesCanBeRemovedAsync(area, companies, cancellationToken);
        (area.Name, area.Code, area.Description) = (name, code, description);
        area.SetCompanies(companies);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await AreaResponseAsync(id, cancellationToken);
    }

    /// <summary>Desactivada, ya no se asigna a nuevos usuarios; los actuales conservan su asignación.</summary>
    public async Task<AreaAdminResponse> SetAreaStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var area = await reader.FindAreaAsync(id, cancellationToken) ?? throw new NotFoundException("El área no existe.");
        area.IsActive = isActive;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await AreaResponseAsync(id, cancellationToken);
    }

    // ——— Validación ———

    private async Task<(string Code, string Name, string Ruc, string Email)> ValidateCompanyAsync(SaveCompanyCommand request, Guid? id, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();
        var ruc = request.Ruc.Trim();
        var email = request.BillingEmail.Trim().ToLowerInvariant();
        if (!CompanyCode().IsMatch(code)) throw new ValidationException("El código debe tener de 2 a 5 letras o números.");
        if (name.Length == 0) throw new ValidationException("Ingresa la razón social.");
        if (ruc.Length != 11 || !ruc.All(char.IsAsciiDigit)) throw new ValidationException("El RUC debe tener 11 dígitos.");
        if (!ruc.StartsWith("20", StringComparison.Ordinal)) throw new ValidationException("El RUC de una empresa empieza con 20.");
        if (!MailAddress.TryCreate(email, out _)) throw new ValidationException("Ingresa un correo válido.");
        if (await organization.CompanyCodeExistsAsync(code, id, cancellationToken)) throw new ConflictException($"El código {code} ya está en uso.");
        if (await organization.CompanyRucExistsAsync(ruc, id, cancellationToken)) throw new ConflictException("Ya existe una sociedad con este RUC.");
        return (code, name, ruc, email);
    }

    private async Task<(IReadOnlyList<Company> Companies, string Name, string Code, string? Description)> ValidateAreaAsync(SaveAreaCommand request, Area? current, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (name.Length == 0) throw new ValidationException("Ingresa el nombre del área.");
        var code = Area.CodeFor(name);
        if (code.Length == 0) throw new ValidationException("El nombre del área debe tener letras o números.");
        if (request.CompanyIds.Count == 0) throw new ValidationException("Selecciona al menos una sociedad.");
        var companies = new List<Company>();
        foreach (var companyId in request.CompanyIds.Distinct())
        {
            var company = await reader.FindCompanyAsync(companyId, cancellationToken) ?? throw new ValidationException("Una de las sociedades seleccionadas no es válida.");
            // Una sociedad inactiva no se agrega, pero el área que ya la tiene la conserva.
            if (!company.IsActive && current?.HasCompany(company.Id) != true) throw new ValidationException($"La sociedad {company.Name} está inactiva.");
            companies.Add(company);
        }
        if (await organization.AreaCodeExistsAsync(code, current?.Id, cancellationToken)) throw new ConflictException($"Ya existe un área llamada {name}.");
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        return (companies, name, code, description);
    }

    /// <summary>Quitar sociedades no puede dejar usuarios del área sin sociedad en común ni documentos pendientes sin aprobador.</summary>
    private async Task EnsureCompaniesCanBeRemovedAsync(Area area, IReadOnlyList<Company> companies, CancellationToken cancellationToken)
    {
        var removed = area.Companies.Where(item => companies.All(company => company.Id != item.CompanyId)).ToList();
        if (removed.Count == 0) return;
        var names = string.Join(", ", removed.Select(item => item.Company.Name));
        var outside = await organization.AreaUsersOutsideCompaniesAsync(area.Id, companies.Select(company => company.Id).ToArray(), cancellationToken);
        if (outside.Count > 0)
            throw new ConflictException($"No se puede quitar {names}: {string.Join(", ", outside)} quedaría(n) sin sociedades en común con el área. Cambia primero sus sociedades o su área.");
        var pending = await organization.PendingApprovalsAsync(area.Id, removed.Select(item => item.CompanyId).ToArray(), cancellationToken);
        if (pending > 0)
            throw new ConflictException($"No se puede quitar {names}: el área tiene {pending} documento(s) pendiente(s) de aprobación en esa sociedad.");
    }

    private async Task<CompanyAdminResponse> CompanyResponseAsync(Guid id, CancellationToken cancellationToken) =>
        ToResponse((await reader.ListCompaniesAsync(cancellationToken)).Single(item => item.Company.Id == id));

    private async Task<AreaAdminResponse> AreaResponseAsync(Guid id, CancellationToken cancellationToken) =>
        ToResponse((await reader.ListAreasAsync(cancellationToken)).Single(item => item.Area.Id == id));

    private static CompanyAdminResponse ToResponse(CompanySummary item) => new(
        item.Company.Id, item.Company.Code, item.Company.Name, item.Company.Ruc, item.Company.BillingEmail, item.Company.IsActive, item.AreaCount, item.UserCount);

    private static AreaAdminResponse ToResponse(AreaSummary item) => new(
        item.Area.Id, item.Area.Name, item.Area.Description,
        item.Area.Companies.OrderBy(company => company.Company.Code)
            .Select(company => new AreaCompanyResponse(company.CompanyId, company.Company.Code, company.Company.Name, company.Company.IsActive)).ToArray(),
        item.Area.IsActive, item.UserCount);

    private static bool Contains(string? value, string term) => value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;

    [GeneratedRegex("^[A-Z0-9]{2,5}$")]
    private static partial Regex CompanyCode();
}
