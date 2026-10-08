using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebProveedores.Api.Security;
using WebProveedores.Application.Contracts.Auth.Requests;
using WebProveedores.Application.Contracts.Auth.Responses;
using WebProveedores.Application.Ports.Inbound.Auth;
using WebProveedores.Domain.Identity;

namespace WebProveedores.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    ILoginService login,
    IPasswordService passwords,
    IProviderRegistrationService registration,
    ICurrentUser currentUser,
    ILogger<AuthController> logger) : ControllerBase
{
    /// <summary>Alta directa de proveedores. El registro de proveedores es por RUC (request-access-key); esta vía es solo administrativa.</summary>
    [HttpPost("register")]
    [Authorize(Policy = Policies.UsersManage)]
    public async Task<ActionResult<UserResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        return Ok(await registration.RegisterAsync(request, cancellationToken));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        // Una cuenta bloqueada lanza AccountLockedException: el manejador global responde 429 con Retry-After.
        var response = await login.LoginAsync(request, cancellationToken);
        return response is null ? Unauthorized(new { message = "RUC, usuario o contraseña inválidos." }) : Ok(response);
    }

    [HttpPost("validate-ruc")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<ActionResult<ProviderLookupResponse>> ValidateRuc(ValidateRucRequest request, CancellationToken cancellationToken)
    {
        return Ok(await registration.ValidateRucAsync(request.Ruc, cancellationToken));
    }

    [HttpPost("request-access-key")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<ActionResult<AccessKeyResponse>> RequestAccessKey(RequestAccessKeyRequest request, CancellationToken cancellationToken)
    {
        return Ok(await registration.RequestAccessKeyAsync(request.Ruc, cancellationToken));
    }

    /// <summary>
    /// Responde siempre lo mismo, exista o no el RUC, y sin devolver el correo (ni siquiera ofuscado),
    /// para que no sirva para averiguar qué RUC tienen cuenta.
    /// </summary>
    [HttpPost("password-reset/request")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<ActionResult<PasswordResetResponse>> RequestPasswordReset(PasswordResetRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await passwords.RequestPasswordResetAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Un fallo de correo no debe delatar que la cuenta existe: se registra y se responde igual.
            logger.LogError(exception, "No se pudo procesar la recuperación de contraseña");
        }
        return Ok(new PasswordResetResponse(true, string.Empty));
    }

    [HttpPost("password-reset/confirm")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<IActionResult> ConfirmPasswordReset(PasswordResetConfirmRequest request, CancellationToken cancellationToken)
    {
        var confirmed = await passwords.ConfirmPasswordResetAsync(request, PasswordTokenPurpose.PasswordReset, cancellationToken);
        return confirmed ? NoContent() : BadRequest(new { message = "El enlace de recuperación es inválido o ya venció." });
    }

    [HttpPost("activation/confirm")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<IActionResult> ConfirmActivation(PasswordResetConfirmRequest request, CancellationToken cancellationToken)
    {
        var confirmed = await passwords.ConfirmPasswordResetAsync(request, PasswordTokenPurpose.Activation, cancellationToken);
        return confirmed ? NoContent() : BadRequest(new { message = "El enlace de activación es inválido o ya venció." });
    }

    /// <summary>Cambia la contraseña de quien tiene sesión y devuelve una sesión nueva.</summary>
    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<ActionResult<AuthResponse>> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken) =>
        Ok(await passwords.ChangePasswordAsync(currentUser.Id, currentUser.IsPasswordChangeSession, request, cancellationToken));

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken) =>
        await login.GetCurrentUserAsync(currentUser.Id, cancellationToken) is { } user ? Ok(user) : Unauthorized();
}
