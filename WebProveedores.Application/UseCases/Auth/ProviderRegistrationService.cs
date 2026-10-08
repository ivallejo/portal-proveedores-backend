using System.Text.RegularExpressions;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Application.Contracts.Auth.Commands;
using WebProveedores.Application.Contracts.Auth.Responses;
using WebProveedores.Application.Ports.Inbound.Auth;
using WebProveedores.Application.Ports.Outbound.Persistence;
using WebProveedores.Application.Ports.Outbound.Sap;
using WebProveedores.Application.Ports.Outbound.Sap.Models;
using WebProveedores.Application.Ports.Outbound.Security;
using WebProveedores.Domain.Access;
using WebProveedores.Domain.Identity;

namespace WebProveedores.Application.UseCases.Auth;

/// <summary>
/// Alta de proveedores: registro online por RUC (consulta SAP y envía el enlace de activación)
/// y alta directa con contraseña, reservada al administrador.
/// </summary>
internal sealed partial class ProviderRegistrationService(
    IUserRepository users,
    IUserUniquenessChecker uniqueness,
    IRoleReader roleReader,
    ICompanyReader companyReader,
    IUnitOfWork unitOfWork,
    IPasswordHasher hasher,
    PasswordLinks links,
    IProviderDirectory sapProvider,
    TimeProvider clock) : IProviderRegistrationService
{
    public async Task<ProviderLookupResponse> ValidateRucAsync(string ruc, CancellationToken cancellationToken)
    {
        var normalizedRuc = NormalizeRuc(ruc);
        // Una cuenta que nunca activó su contraseña (el correo no llegó o expiró) puede volver a registrarse.
        var existing = await users.FindByRucAsync(normalizedRuc, cancellationToken);
        if (existing?.PasswordSetAtUtc is not null)
            throw new ConflictException("El usuario ya se encuentra registrado.");

        var provider = await FindProviderAsync(normalizedRuc, cancellationToken);
        return new(normalizedRuc, provider.CompanyName, ObfuscateEmail(provider.Correo!));
    }

    public async Task<AccessKeyResponse> RequestAccessKeyAsync(string ruc, CancellationToken cancellationToken)
    {
        var normalizedRuc = NormalizeRuc(ruc);
        var provider = await FindProviderAsync(normalizedRuc, cancellationToken);
        var user = await users.FindByRucAsync(normalizedRuc, cancellationToken);
        if (user?.PasswordSetAtUtc is not null)
            throw new ConflictException("Este RUC ya tiene una cuenta activa. Usa la opción 'Olvidé mi contraseña' para recuperar el acceso.");

        var now = clock.GetUtcNow().UtcDateTime;
        var email = provider.Correo!.Trim().ToLowerInvariant();
        if (user is null)
        {
            // Hasta activar la cuenta tiene una contraseña aleatoria que nadie conoce.
            user = AppUser.Create(normalizedRuc, provider.CompanyName, normalizedRuc, email, hasher.Hash(AuthSupport.NewOneTimeToken()), now, activated: false);
            await MakeProviderAsync(user, cancellationToken);
            users.Add(user);
        }
        else
        {
            user.RefreshPendingProvider(provider.CompanyName, email, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        try
        {
            await links.SendAsync(user, PasswordTokenPurpose.Activation, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // El token ya quedó guardado: al reintentar se genera uno nuevo y se vuelve a enviar.
            throw new ServiceUnavailableException("No pudimos enviar el correo de activación. Intenta nuevamente en unos minutos.", exception);
        }
        return new(true, ObfuscateEmail(provider.Correo!));
    }

    public async Task<UserResponse> RegisterAsync(RegisterProviderCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var ruc = request.Ruc.Trim();
        if (await uniqueness.EmailExistsAsync(email, cancellationToken) || await uniqueness.RucExistsAsync(ruc, cancellationToken))
            throw new ConflictException("Ya existe un usuario registrado con ese correo o RUC.");

        var user = AppUser.Create(ruc, request.CompanyName, ruc, email, hasher.Hash(request.Password), clock.GetUtcNow().UtcDateTime);
        await MakeProviderAsync(user, cancellationToken);
        users.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return AuthSupport.ToResponse(user);
    }

    /// <summary>Rol de proveedor y, por defecto, todas las sociedades (el administrador puede restringirlas después).</summary>
    private async Task MakeProviderAsync(AppUser user, CancellationToken cancellationToken)
    {
        var role = await roleReader.FindRoleAsync(SecurityCatalog.ProviderRole, cancellationToken)
            ?? throw new InvalidOperationException("El rol de proveedor no está configurado.");
        user.SetRoles([role]);
        user.SetCompanies(await companyReader.ListActiveAsync(cancellationToken));
    }

    private async Task<SapProviderRecord> FindProviderAsync(string ruc, CancellationToken cancellationToken)
    {
        var provider = await sapProvider.FindByRucAsync(ruc, cancellationToken);
        if (provider is null || string.IsNullOrWhiteSpace(provider.CompanyName))
            throw new NotFoundException("No encontramos información para el RUC indicado.");
        // El enlace de activación va al correo registrado en SAP: sin él no se puede completar el registro.
        if (string.IsNullOrWhiteSpace(provider.Correo))
            throw new ValidationException("Tu empresa está registrada como proveedor, pero no tiene un correo de contacto en nuestro sistema. Comunícate con el área de Compras para actualizarlo.");
        return provider;
    }

    private static string NormalizeRuc(string ruc) =>
        RucPattern().IsMatch(ruc.Trim()) ? ruc.Trim() : throw new NotFoundException("Ingresa un RUC válido de 11 dígitos.");

    private static string ObfuscateEmail(string email)
    {
        var parts = email.Split('@', 2);
        if (parts.Length != 2) return email;
        var domainParts = parts[1].Split('.', 2);
        var domainName = domainParts[0];
        var suffix = domainParts.Length > 1 ? $".{domainParts[1]}" : string.Empty;
        return $"{parts[0][..Math.Min(3, parts[0].Length)]}*****{domainName[^Math.Min(3, domainName.Length)..]}{suffix}";
    }

    [GeneratedRegex(@"^\d{11}$")]
    private static partial Regex RucPattern();
}
