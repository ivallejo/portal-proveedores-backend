using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Application.Contracts.Admin.Commands;
using WebProveedores.Application.Contracts.Admin.Responses;
using WebProveedores.Application.Ports.Inbound.Admin;
using WebProveedores.Application.Ports.Outbound.Persistence;
using WebProveedores.Application.Ports.Outbound.Persistence.Models;
using WebProveedores.Application.Ports.Outbound.Security;
using WebProveedores.Application.UseCases.Auth;
using WebProveedores.Application.UseCases.Profile;
using WebProveedores.Domain.Access;
using WebProveedores.Domain.Common;
using WebProveedores.Domain.Identity;
using WebProveedores.Domain.Organization;

namespace WebProveedores.Application.UseCases.Admin;

/// <summary>
/// Configuración › Usuarios. Un rol por usuario. Proveedor: RUC (10/20…) y razón social; personal interno: DNI,
/// nombres, apellidos y área de una de sus sociedades. Al menos un correo (uno principal) y una sociedad.
/// La cuenta nueva se activa con un enlace al correo principal. Nunca queda el portal sin un administrador activo.
/// </summary>
internal sealed class AdminUserService(
    IUserRepository users,
    IUserQueries userQueries,
    IUserUniquenessChecker uniqueness,
    IRoleReader roleReader,
    IOrganizationReader organization,
    IPasswordTokenRepository passwordTokens,
    IUnitOfWork unitOfWork,
    IPasswordHasher hasher,
    PasswordLinks passwordLinks,
    EmailVerifications verifications,
    TimeProvider clock) : IAdminUserService
{
    public async Task<AdminUserPage> SearchAsync(string? search, string? role, string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var now = Now;
        var filter = new UserSearchFilter(search, string.IsNullOrWhiteSpace(role) ? null : role.Trim(), ParseStatusFilter(status), now);
        var result = await userQueries.SearchAsync(filter, page, pageSize, cancellationToken);
        var (total, active) = await userQueries.CountByStatusAsync(now, cancellationToken);
        return new AdminUserPage(result.Items.Select(ToSummary).ToArray(), result.Total, page, pageSize, new AdminUserCounts(total, active, total - active));
    }

    public async Task<AdminCatalogResponse> CatalogAsync(CancellationToken cancellationToken)
    {
        var order = SecurityCatalog.Roles.Keys.ToList();
        var roles = (await roleReader.ListRolesAsync(cancellationToken))
            .OrderBy(role => order.IndexOf(role.Code) is var index && index < 0 ? int.MaxValue : index)
            .Select(role => new AdminRoleOption(role.Code, role.Name, role.Description, role.Code == SecurityCatalog.ProviderRole, role.IsActive))
            .ToArray();
        var areas = (await organization.ListAreasAsync(cancellationToken))
            .Select(item => new AdminAreaOption(item.Area.Id, item.Area.Name, item.Area.Company.Code, item.Area.Company.Name, item.Area.IsActive))
            .OrderBy(area => area.CompanyCode).ThenBy(area => area.Name).ToArray();
        var companies = (await organization.ListCompaniesAsync(cancellationToken))
            .Select(item => new AdminCompanyOption(item.Company.Code, item.Company.Name, item.Company.Ruc, item.Company.IsActive))
            .OrderBy(company => company.Code).ToArray();
        return new AdminCatalogResponse(roles, areas, companies);
    }

    public async Task<AdminUserDetail?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await userQueries.FindByIdAsync(id, cancellationToken) is { } user ? ToDetail(user) : null;

    public async Task<AdminUserDetail> CreateAsync(SaveUserCommand request, CancellationToken cancellationToken)
    {
        var role = await FindRoleAsync(request.Role, cancellationToken);
        var isProvider = role.Code == SecurityCatalog.ProviderRole;
        var document = request.Document?.Trim() ?? string.Empty;
        string name;
        (string First, string Last)? person = null;
        if (isProvider)
        {
            if (document.Length != 11 || !document.All(char.IsAsciiDigit)) throw new ValidationException("El RUC debe tener 11 dígitos.");
            if (!document.StartsWith("10") && !document.StartsWith("20")) throw new ValidationException("El RUC debe empezar con 10 o 20.");
            if (await uniqueness.RucExistsAsync(document, cancellationToken) || await uniqueness.UsernameExistsAsync(document, cancellationToken))
                throw new ConflictException("Ya existe un usuario con este documento.");
            name = BusinessName(request);
        }
        else
        {
            if (document.Length != 8 || !document.All(char.IsAsciiDigit)) throw new ValidationException("El DNI debe tener 8 dígitos.");
            if (await uniqueness.DniExistsAsync(document, cancellationToken)) throw new ConflictException("Ya existe un usuario con este documento.");
            person = PersonName(request);
            name = $"{person.Value.First} {person.Value.Last}";
        }

        var emails = NormalizeEmails(request.Emails);
        foreach (var email in emails)
            if (await uniqueness.EmailExistsAsync(email.Email, cancellationToken)) throw new ConflictException($"El correo {email.Email} ya pertenece a otro usuario.");

        var now = Now;
        var primary = emails.Single(email => email.IsPrimary);
        // Hasta activar la cuenta tiene una contraseña aleatoria que nadie conoce.
        var user = AppUser.Create(document, name, isProvider ? document : null, primary.Email, hasher.Hash(AuthSupport.NewOneTimeToken()), now,
            activated: false, dni: isProvider ? null : document, emailVerified: false);
        if (person is { } names) user.SetPersonName(names.First, names.Last, now);
        user.SetEmailType(user.Emails.Single().Id, primary.Type, now);
        var pending = emails.Where(email => !email.IsPrimary).Select(email => user.AddEmail(email.Email, email.Type, now)).ToList();
        await ApplyAccessAsync(user, role, request.AreaId, request.CompanyCodes, cancellationToken);
        var tokens = pending.Select(email => (Email: email, Token: verifications.Start(email))).ToList();
        users.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await passwordLinks.SendAsync(user, PasswordTokenPurpose.Activation, cancellationToken);
        foreach (var (email, token) in tokens) await verifications.SendAsync(user.CompanyName, email, token, cancellationToken);
        return ToDetail(user);
    }

    public async Task<AdminUserDetail?> UpdateAsync(Guid actorId, Guid id, SaveUserCommand request, CancellationToken cancellationToken)
    {
        var user = await users.FindTrackedByIdAsync(id, cancellationToken);
        if (user is null) return null;
        var now = Now;
        var role = await FindRoleAsync(request.Role, cancellationToken);
        if ((role.Code == SecurityCatalog.ProviderRole) != user.IsProvider)
            throw new ValidationException(user.IsProvider ? "Un proveedor solo puede tener el rol Proveedor." : "El rol Proveedor es solo para cuentas con RUC.");

        var losesAdministrator = IsAdministrator(user) && role.Code != SecurityCatalog.AdministratorRole;
        if (losesAdministrator && user.Id == actorId) throw new ConflictException("No puedes quitarte el rol de administrador.");
        var deactivates = user.IsActive && ParseStatus(request.Status, user) == UserStatus.Inactive;
        if (deactivates && user.Id == actorId) throw new ConflictException("No puedes desactivar tu propia cuenta.");
        if ((losesAdministrator || (deactivates && IsAdministrator(user))) && user.IsActive) await EnsureAnotherAdministratorAsync(user.Id, cancellationToken);

        if (user.IsProvider) user.RenameBusiness(BusinessName(request), now);
        else
        {
            var (first, last) = PersonName(request);
            user.SetPersonName(first, last, now);
        }
        await ApplyAccessAsync(user, role, request.AreaId, request.CompanyCodes, cancellationToken);
        user.RequirePasswordChange(request.MustChangePassword, now);
        ApplyStatus(user, ParseStatus(request.Status, user), now);

        var emails = NormalizeEmails(request.Emails);
        foreach (var email in emails.Where(email => email.Id is null))
            if (await uniqueness.EmailUsedByOtherAsync(email.Email, user.Id, cancellationToken)) throw new ConflictException($"El correo {email.Email} ya pertenece a otro usuario.");
        var verificationsToSend = new List<(UserEmail Email, string Token)>();
        await unitOfWork.InTransactionAsync(async () =>
        {
            var target = ApplyEmails(user, emails, now, verificationsToSend);
            // Un solo principal por usuario (índice único): primero se guarda sin principal y luego se marca el nuevo.
            await unitOfWork.SaveChangesAsync(cancellationToken);
            target.IsPrimary = true;
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        foreach (var (email, token) in verificationsToSend) await verifications.SendAsync(user.CompanyName, email, token, cancellationToken);
        return ToDetail(user);
    }

    public async Task<AdminUserDetail?> SetStatusAsync(Guid actorId, Guid id, SetUserStatusCommand request, CancellationToken cancellationToken)
    {
        var user = await users.FindTrackedByIdAsync(id, cancellationToken);
        if (user is null) return null;
        if (!request.IsActive && user.IsActive)
        {
            if (user.Id == actorId) throw new ConflictException("No puedes desactivar tu propia cuenta.");
            if (IsAdministrator(user)) await EnsureAnotherAdministratorAsync(user.Id, cancellationToken);
        }
        ApplyStatus(user, request.IsActive ? UserStatus.Active : UserStatus.Inactive, Now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDetail(user);
    }

    public async Task<PasswordLinkSent?> SendPasswordLinkAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await users.FindTrackedByIdAsync(id, cancellationToken);
        if (user is null) return null;
        if (!user.IsActive) throw new ConflictException("Activa la cuenta antes de enviarle un enlace.");
        var purpose = user.IsActivated ? PasswordTokenPurpose.PasswordReset : PasswordTokenPurpose.Activation;
        var email = await passwordLinks.SendAsync(user, purpose, cancellationToken);
        return new PasswordLinkSent(KindOf(purpose), email);
    }

    public async Task<IReadOnlyList<PasswordLinkResponse>?> PasswordLinksAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await userQueries.FindByIdAsync(id, cancellationToken) is null) return null;
        var now = Now;
        return (await passwordTokens.ListForUserAsync(id, cancellationToken))
            .Select(token => new PasswordLinkResponse(
                KindOf(token.Purpose),
                $"{token.TokenHash[..4]}…{token.TokenHash[^4..]}",
                token.CreatedAtUtc,
                token.ExpiresAtUtc,
                token.UsedAtUtc is not null ? "used" : token.RevokedAtUtc is not null ? "replaced" : token.ExpiresAtUtc <= now ? "expired" : "valid"))
            .ToArray();
    }

    // ——— Reglas ———

    private async Task<Role> FindRoleAsync(string code, CancellationToken cancellationToken)
    {
        var role = await roleReader.FindRoleAsync(code.Trim(), cancellationToken);
        if (role is null) throw new ValidationException("Selecciona el rol.");
        if (!role.IsActive) throw new ValidationException("El rol seleccionado está inactivo.");
        return role;
    }

    private async Task ApplyAccessAsync(AppUser user, Role role, Guid? areaId, IReadOnlyList<string> companyCodes, CancellationToken cancellationToken)
    {
        var all = (await organization.ListCompaniesAsync(cancellationToken)).Select(item => item.Company).ToList();
        var wanted = companyCodes.Select(code => code.Trim()).Where(code => code.Length > 0).Distinct().ToArray();
        if (wanted.Length == 0) throw new ValidationException("Asigna al menos una sociedad en la pestaña Sociedades.");
        var current = user.UserCompanies.Select(item => item.CompanyId).ToHashSet();
        var companies = new List<Company>();
        foreach (var code in wanted)
        {
            var company = all.FirstOrDefault(item => item.Code == code) ?? throw new ValidationException($"Sociedad no válida: {code}.");
            // Una sociedad inactiva no se asigna, pero quien ya la tiene la conserva.
            if (!company.IsActive && !current.Contains(company.Id)) throw new ValidationException($"La sociedad {company.Name} está inactiva.");
            companies.Add(company);
        }

        Area? area = null;
        if (role.Code != SecurityCatalog.ProviderRole)
        {
            if (areaId is { } value)
            {
                area = await organization.FindAreaAsync(value, cancellationToken) ?? throw new ValidationException("El área seleccionada no es válida.");
                if (!area.IsActive && user.AreaId != area.Id) throw new ValidationException("El área seleccionada está inactiva.");
                if (companies.All(company => company.Id != area.CompanyId))
                    throw new ValidationException($"El área {area.Name} es de {area.Company.Name}: asigna también esa sociedad.");
            }
            // El administrador puede no pertenecer a un área; el resto del personal interno, sí.
            else if (role.Code != SecurityCatalog.AdministratorRole) throw new ValidationException("Selecciona el área.");
        }

        user.SetRoles([role]);
        user.AssignArea(area);
        user.SetCompanies(companies);
    }

    /// <summary>Correos válidos, sin repetir y con exactamente un principal.</summary>
    private static List<(Guid? Id, string Email, EmailType Type, bool IsPrimary)> NormalizeEmails(IReadOnlyList<UserEmailData> input)
    {
        var emails = input.Select(item => (item.Id, Email: item.Email.Trim().ToLowerInvariant(), Type: ProfileService.ParseType(item.Type), item.IsPrimary)).ToList();
        if (emails.Count == 0) throw new ValidationException("Agrega al menos un correo en la pestaña Correos.");
        var invalid = emails.FirstOrDefault(item => !ProfileService.IsEmail(item.Email));
        if (invalid.Email is not null) throw new ValidationException($"El correo {invalid.Email} no es válido.");
        if (emails.Select(item => item.Email).Distinct().Count() != emails.Count) throw new ValidationException("Hay correos repetidos.");
        if (emails.Count(item => item.IsPrimary) != 1) throw new ValidationException("Marca un correo como principal.");
        return emails;
    }

    /// <summary>Quita, agrega y actualiza correos. Deja todos sin principal y devuelve el que debe serlo.</summary>
    private UserEmail ApplyEmails(AppUser user, List<(Guid? Id, string Email, EmailType Type, bool IsPrimary)> emails, DateTime now, List<(UserEmail, string)> toVerify)
    {
        var keep = emails.Where(item => item.Id is not null).Select(item => item.Id!.Value).ToHashSet();
        foreach (var existing in user.Emails) existing.IsPrimary = false;
        foreach (var removed in user.Emails.Where(item => !keep.Contains(item.Id)).ToList()) user.RemoveEmail(removed.Id, now);

        UserEmail? target = null;
        foreach (var item in emails)
        {
            UserEmail email;
            if (item.Id is { } id)
            {
                email = user.Emails.FirstOrDefault(existing => existing.Id == id) ?? throw new ValidationException("Uno de los correos ya no existe; vuelve a abrir el usuario.");
                if (email.Email != item.Email) throw new ValidationException("Un correo registrado no se puede modificar; elimínalo y agrega el nuevo.");
                user.SetEmailType(id, item.Type, now);
            }
            else
            {
                email = user.AddEmail(item.Email, item.Type, now);
                toVerify.Add((email, verifications.Start(email)));
            }
            if (item.IsPrimary) target = email;
        }

        // El principal recibe las notificaciones: debe estar verificado (salvo que la cuenta aún no se active,
        // porque activarla verifica el correo principal).
        if (!target!.IsVerified && user.IsActivated) throw new DomainRuleException("Verifica el correo antes de hacerlo principal.");
        return target;
    }

    private static void ApplyStatus(AppUser user, UserStatus status, DateTime now)
    {
        switch (status)
        {
            case UserStatus.Inactive:
                user.SetActive(false, now);
                break;
            case UserStatus.Active:
                user.SetActive(true, now);
                user.Unlock(now);
                break;
        }
        // Locked: se deja el bloqueo vigente como está.
    }

    private UserStatus ParseStatus(string? value, AppUser user) => value?.Trim().ToLowerInvariant() switch
    {
        "inactive" => UserStatus.Inactive,
        "locked" when user.StatusAt(Now) == UserStatus.Locked => UserStatus.Locked,
        "locked" => throw new ValidationException("El bloqueo solo ocurre tras varios intentos fallidos; elige Activo o Inactivo."),
        _ => UserStatus.Active,
    };

    private static UserStatus? ParseStatusFilter(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "active" => UserStatus.Active,
        "inactive" => UserStatus.Inactive,
        "locked" => UserStatus.Locked,
        _ => null,
    };

    private static string BusinessName(SaveUserCommand request)
    {
        var name = request.BusinessName?.Trim() ?? string.Empty;
        if (name.Length == 0) throw new ValidationException("Ingresa la razón social.");
        return name;
    }

    private static (string First, string Last) PersonName(SaveUserCommand request)
    {
        var first = request.FirstName?.Trim() ?? string.Empty;
        var last = request.LastName?.Trim() ?? string.Empty;
        if (first.Length == 0) throw new ValidationException("Ingresa los nombres.");
        if (last.Length == 0) throw new ValidationException("Ingresa los apellidos.");
        if (!ProfileService.PersonName().IsMatch(first) || !ProfileService.PersonName().IsMatch(last))
            throw new ValidationException("Los nombres y apellidos solo pueden tener letras.");
        return (first, last);
    }

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private async Task EnsureAnotherAdministratorAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!await userQueries.OtherActiveAdministratorExistsAsync(userId, cancellationToken))
            throw new ConflictException("Debe quedar al menos un administrador activo.");
    }

    private static bool IsAdministrator(AppUser user) => user.HasRole(SecurityCatalog.AdministratorRole);

    private static string KindOf(PasswordTokenPurpose purpose) => purpose == PasswordTokenPurpose.Activation ? "activation" : "reset";

    private static string StatusCode(UserStatus status) => status switch
    {
        UserStatus.Inactive => "inactive",
        UserStatus.Locked => "locked",
        _ => "active",
    };

    private static (string Document, string Type) DocumentOf(AppUser user) =>
        user.IsProvider ? (user.Ruc!, "RUC") : user.Dni is { } dni ? (dni, "DNI") : (user.Username, "Usuario");

    private Role? RoleOf(AppUser user) => user.UserRoles.Select(item => item.Role).FirstOrDefault();

    private AdminUserSummary ToSummary(AppUser user)
    {
        var (document, type) = DocumentOf(user);
        var role = RoleOf(user);
        return new AdminUserSummary(user.Id, user.CompanyName, user.PrimaryEmail, user.IsProvider, document, type, role?.Code, role?.Name,
            user.Area?.Name, user.UserCompanies.Select(item => item.Company.Code).Order().ToArray(), StatusCode(user.StatusAt(Now)), user.IsActivated);
    }

    private AdminUserDetail ToDetail(AppUser user)
    {
        var (document, type) = DocumentOf(user);
        return new AdminUserDetail(
            user.Id, user.Username, user.IsProvider, document, type, user.CompanyName,
            user.IsProvider ? user.CompanyName : null,
            user.IsProvider ? null : user.FirstName ?? user.CompanyName,
            user.IsProvider ? null : user.LastName ?? string.Empty,
            RoleOf(user)?.Code, user.AreaId,
            user.UserCompanies.Select(item => item.Company.Code).Order().ToArray(),
            user.Emails.OrderByDescending(item => item.IsPrimary).ThenBy(item => item.CreatedAtUtc)
                .Select(item => new AdminUserEmail(item.Id, item.Email, ProfileService.TypeCode(item.Type), item.IsPrimary, item.IsVerified, item.CreatedAtUtc)).ToArray(),
            StatusCode(user.StatusAt(Now)), user.IsActivated, user.MustChangePassword, user.CreatedAtUtc, user.UpdatedAtUtc ?? user.CreatedAtUtc);
    }
}
