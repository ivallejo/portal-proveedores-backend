using System.Net.Mail;
using System.Text.RegularExpressions;
using WebProveedores.Application.Auth;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Application.Ports.Outbound.Persistence;
using WebProveedores.Domain.Identity;

namespace WebProveedores.Application.Profile;

internal sealed partial class ProfileService(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    EmailVerifications verifications,
    TimeProvider clock) : IProfileService
{
    public async Task<ProfileResponse> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        ToResponse(await users.FindByIdAsync(userId, cancellationToken) ?? throw new ForbiddenException("La sesión no es válida."));

    public async Task<ProfileResponse> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await LoadAsync(userId, cancellationToken);
        var now = Now();
        if (user.IsProvider)
        {
            var name = request.BusinessName?.Trim() ?? string.Empty;
            if (name.Length < 3) throw new ValidationException("La razón social debe tener al menos 3 caracteres.");
            user.RenameBusiness(name, now);
        }
        else
        {
            var firstName = request.FirstName?.Trim() ?? string.Empty;
            var lastName = request.LastName?.Trim() ?? string.Empty;
            if (firstName.Length == 0) throw new ValidationException("Ingresa tus nombres.");
            if (lastName.Length == 0) throw new ValidationException("Ingresa tus apellidos.");
            if (!PersonName().IsMatch(firstName) || !PersonName().IsMatch(lastName))
                throw new ValidationException("Los nombres y apellidos solo pueden tener letras.");
            user.SetPersonName(firstName, lastName, now);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    public async Task<ProfileResponse> AddEmailAsync(Guid userId, AddEmailRequest request, CancellationToken cancellationToken)
    {
        var user = await LoadAsync(userId, cancellationToken);
        var address = request.Email.Trim().ToLowerInvariant();
        if (!IsEmail(address)) throw new ValidationException("Ingresa un correo válido, por ejemplo nombre@empresa.com.");
        var type = ParseType(request.Type);
        if (user.Emails.Any(item => item.Email == address)) throw new ConflictException("Este correo ya está registrado en tu perfil.");
        if (await users.EmailExistsAsync(address, cancellationToken)) throw new ConflictException("Este correo ya está registrado en otra cuenta.");

        var email = user.AddEmail(address, type, Now());
        var token = verifications.Start(email);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await verifications.SendAsync(user.CompanyName, email, token, cancellationToken);
        return ToResponse(user);
    }

    public async Task<ProfileResponse> ResendVerificationAsync(Guid userId, Guid emailId, CancellationToken cancellationToken)
    {
        var user = await LoadAsync(userId, cancellationToken);
        var email = FindEmail(user, emailId);
        if (email.IsVerified) throw new ConflictException("El correo ya está verificado.");
        var token = verifications.Start(email);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await verifications.SendAsync(user.CompanyName, email, token, cancellationToken);
        return ToResponse(user);
    }

    public async Task<ProfileResponse> MakePrimaryAsync(Guid userId, Guid emailId, CancellationToken cancellationToken)
    {
        var user = await LoadAsync(userId, cancellationToken);
        var email = FindEmail(user, emailId);
        user.MakePrimary(email.Id, Now());
        // La base admite un solo principal por usuario y no garantiza el orden de las actualizaciones:
        // primero se quita el principal anterior y luego se marca el nuevo, en una sola transacción.
        await unitOfWork.InTransactionAsync(async () =>
        {
            email.IsPrimary = false;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            email.IsPrimary = true;
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
        return ToResponse(user);
    }

    public async Task<ProfileResponse> RemoveEmailAsync(Guid userId, Guid emailId, CancellationToken cancellationToken)
    {
        var user = await LoadAsync(userId, cancellationToken);
        user.RemoveEmail(FindEmail(user, emailId).Id, Now());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    public async Task<string?> VerifyEmailAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var email = await users.FindEmailByVerificationTokenAsync(AuthSupport.HashOneTimeToken(token.Trim()), Now(), cancellationToken);
        if (email is null) return null;
        email.Verify(Now());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return email.Email;
    }

    private async Task<AppUser> LoadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindTrackedByIdAsync(userId, cancellationToken);
        return user is { IsActive: true } ? user : throw new ForbiddenException("La sesión no es válida.");
    }

    private static UserEmail FindEmail(AppUser user, Guid emailId) =>
        user.Emails.FirstOrDefault(item => item.Id == emailId) ?? throw new NotFoundException("El correo no pertenece a tu perfil.");

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;

    internal static bool IsEmail(string value) =>
        MailAddress.TryCreate(value, out var parsed) && parsed.Address == value && value.Split('@')[1].Contains('.');

    internal static EmailType ParseType(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "work" => EmailType.Work,
        "billing" => EmailType.Billing,
        "personal" => EmailType.Personal,
        _ => throw new ValidationException("Elige el tipo de correo: trabajo, facturación o personal."),
    };

    internal static string TypeCode(EmailType type) => type switch
    {
        EmailType.Billing => "billing",
        EmailType.Personal => "personal",
        _ => "work",
    };

    private static ProfileResponse ToResponse(AppUser user) => new(
        user.Username,
        user.IsProvider,
        user.Ruc,
        user.CompanyName,
        user.IsProvider ? user.CompanyName : null,
        user.IsProvider ? null : user.FirstName ?? user.CompanyName,
        user.IsProvider ? null : user.LastName ?? string.Empty,
        user.UserRoles.Where(item => item.Role.IsActive).Select(item => item.Role.Name).Order().ToArray(),
        user.Area?.Name,
        user.Area?.Company?.Name,
        user.UserCompanies.Select(item => new ProfileCompanyResponse(item.Company.Code, item.Company.Name, item.Company.Ruc, item.Company.IsActive))
            .OrderBy(item => item.Code).ToArray(),
        user.Emails.OrderByDescending(item => item.IsPrimary).ThenBy(item => item.CreatedAtUtc)
            .Select(item => new ProfileEmailResponse(item.Id, item.Email, TypeCode(item.Type), item.IsPrimary, item.IsVerified, item.CreatedAtUtc)).ToArray(),
        user.MustChangePassword,
        user.CreatedAtUtc,
        user.UpdatedAtUtc ?? user.CreatedAtUtc,
        user.PasswordSetAtUtc);

    [GeneratedRegex(@"^[\p{L}' .-]+$")]
    internal static partial Regex PersonName();
}
