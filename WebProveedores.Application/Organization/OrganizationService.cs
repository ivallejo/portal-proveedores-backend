using System.Net.Mail;
using System.Text.RegularExpressions;
using WebProveedores.Application.Abstractions.Persistence;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Domain.Organization;

namespace WebProveedores.Application.Organization;

/// <summary>
/// Reglas de Configuración › Sociedades y Áreas. No se eliminan registros: se activan o desactivan
/// y el historial (documentos, usuarios) se conserva.
/// </summary>
internal sealed partial class OrganizationService(IOrganizationRepository organization, IUnitOfWork unitOfWork) : IOrganizationService
{
    // ——— Sociedades ———

    public async Task<IReadOnlyList<CompanyAdminResponse>> ListCompaniesAsync(string? search, bool? active, CancellationToken cancellationToken)
    {
        var term = search?.Trim();
        return (await organization.ListCompaniesAsync(cancellationToken))
            .Where(item => active is null || item.Company.IsActive == active)
            .Where(item => string.IsNullOrEmpty(term) || Contains(item.Company.Code, term) || Contains(item.Company.Name, term) || Contains(item.Company.Ruc, term))
            .OrderBy(item => item.Company.Code)
            .Select(ToResponse)
            .ToArray();
    }

    public async Task<CompanyAdminResponse> CreateCompanyAsync(CompanyRequest request, CancellationToken cancellationToken)
    {
        var data = await ValidateCompanyAsync(request, null, cancellationToken);
        var company = new Company { Code = data.Code, Name = data.Name, Ruc = data.Ruc, BillingEmail = data.Email };
        organization.AddCompany(company);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(new CompanySummary(company, 0, 0));
    }

    public async Task<CompanyAdminResponse> UpdateCompanyAsync(Guid id, CompanyRequest request, CancellationToken cancellationToken)
    {
        var company = await organization.FindCompanyAsync(id, cancellationToken) ?? throw new NotFoundException("La sociedad no existe.");
        var data = await ValidateCompanyAsync(request, id, cancellationToken);
        (company.Code, company.Name, company.Ruc, company.BillingEmail) = (data.Code, data.Name, data.Ruc, data.Email);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await CompanyResponseAsync(id, cancellationToken);
    }

    /// <summary>Desactivada, deja de aparecer en los filtros y no se registran documentos para ella; sus áreas e historial se conservan.</summary>
    public async Task<CompanyAdminResponse> SetCompanyStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var company = await organization.FindCompanyAsync(id, cancellationToken) ?? throw new NotFoundException("La sociedad no existe.");
        company.IsActive = isActive;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await CompanyResponseAsync(id, cancellationToken);
    }

    // ——— Áreas ———

    public async Task<IReadOnlyList<AreaAdminResponse>> ListAreasAsync(string? search, bool? active, Guid? companyId, CancellationToken cancellationToken)
    {
        var term = search?.Trim();
        return (await organization.ListAreasAsync(cancellationToken))
            .Where(item => active is null || item.Area.IsActive == active)
            .Where(item => companyId is null || item.Area.CompanyId == companyId)
            .Where(item => string.IsNullOrEmpty(term) || Contains(item.Area.Name, term) || Contains(item.Area.Description, term) || Contains(item.Area.Company.Name, term))
            .OrderBy(item => item.Area.Company.Code).ThenBy(item => item.Area.Name)
            .Select(ToResponse)
            .ToArray();
    }

    public async Task<AreaAdminResponse> CreateAreaAsync(AreaRequest request, CancellationToken cancellationToken)
    {
        var (company, name, code, description) = await ValidateAreaAsync(request, null, cancellationToken);
        var area = new Area { Code = code, Name = name, Description = description, CompanyId = company.Id, Company = company };
        organization.AddArea(area);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(new AreaSummary(area, 0));
    }

    public async Task<AreaAdminResponse> UpdateAreaAsync(Guid id, AreaRequest request, CancellationToken cancellationToken)
    {
        var area = await organization.FindAreaAsync(id, cancellationToken) ?? throw new NotFoundException("El área no existe.");
        var (company, name, code, description) = await ValidateAreaAsync(request, id, cancellationToken);
        (area.Name, area.Code, area.Description, area.CompanyId, area.Company) = (name, code, description, company.Id, company);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await AreaResponseAsync(id, cancellationToken);
    }

    /// <summary>Desactivada, ya no se asigna a nuevos usuarios; los actuales conservan su asignación.</summary>
    public async Task<AreaAdminResponse> SetAreaStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var area = await organization.FindAreaAsync(id, cancellationToken) ?? throw new NotFoundException("El área no existe.");
        area.IsActive = isActive;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await AreaResponseAsync(id, cancellationToken);
    }

    // ——— Validación ———

    private async Task<(string Code, string Name, string Ruc, string Email)> ValidateCompanyAsync(CompanyRequest request, Guid? id, CancellationToken cancellationToken)
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

    private async Task<(Company Company, string Name, string Code, string? Description)> ValidateAreaAsync(AreaRequest request, Guid? id, CancellationToken cancellationToken)
    {
        var company = await organization.FindCompanyAsync(request.CompanyId, cancellationToken) ?? throw new ValidationException("Selecciona la sociedad.");
        var name = request.Name.Trim();
        if (name.Length == 0) throw new ValidationException("Ingresa el nombre del área.");
        var code = Area.CodeFor(name);
        if (code.Length == 0) throw new ValidationException("El nombre del área debe tener letras o números.");
        if (await organization.AreaCodeExistsAsync(company.Id, code, id, cancellationToken)) throw new ConflictException("Esta sociedad ya tiene un área con ese nombre.");
        // Al crear o mover un área, la sociedad debe estar activa; editar un área existente de una sociedad inactiva se permite.
        var current = id is { } areaId ? await organization.FindAreaAsync(areaId, cancellationToken) : null;
        if (!company.IsActive && current?.CompanyId != company.Id) throw new ValidationException($"La sociedad {company.Name} está inactiva.");
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        return (company, name, code, description);
    }

    private async Task<CompanyAdminResponse> CompanyResponseAsync(Guid id, CancellationToken cancellationToken) =>
        ToResponse((await organization.ListCompaniesAsync(cancellationToken)).Single(item => item.Company.Id == id));

    private async Task<AreaAdminResponse> AreaResponseAsync(Guid id, CancellationToken cancellationToken) =>
        ToResponse((await organization.ListAreasAsync(cancellationToken)).Single(item => item.Area.Id == id));

    private static CompanyAdminResponse ToResponse(CompanySummary item) => new(
        item.Company.Id, item.Company.Code, item.Company.Name, item.Company.Ruc, item.Company.BillingEmail, item.Company.IsActive, item.AreaCount, item.UserCount);

    private static AreaAdminResponse ToResponse(AreaSummary item) => new(
        item.Area.Id, item.Area.Name, item.Area.Description, item.Area.CompanyId, item.Area.Company.Code, item.Area.Company.Name, item.Area.IsActive, item.UserCount);

    private static bool Contains(string? value, string term) => value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;

    [GeneratedRegex("^[A-Z0-9]{2,5}$")]
    private static partial Regex CompanyCode();
}
