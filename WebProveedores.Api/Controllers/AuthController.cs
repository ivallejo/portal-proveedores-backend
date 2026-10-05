using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebProveedores.Api.Infrastructure;
using WebProveedores.Application.Auth;
using WebProveedores.Domain.Entities;

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
        try { return Ok(await registration.RegisterAsync(request, cancellationToken)); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await login.LoginAsync(request, cancellationToken);
            return response is null ? Unauthorized(new { message = "RUC, usuario o contraseña inválidos." }) : Ok(response);
        }
        catch (AccountLockedException exception)
        {
            Response.Headers.RetryAfter = ((int)Math.Ceiling(exception.RetryAfter.TotalSeconds)).ToString();
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = $"Demasiados intentos fallidos. Vuelve a intentarlo en {Math.Max(1, (int)Math.Ceiling(exception.RetryAfter.TotalMinutes))} minuto(s) o recupera tu contraseña." });
        }
    }

    [HttpPost("validate-ruc")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<ActionResult<ProviderLookupResponse>> ValidateRuc(ValidateRucRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await registration.ValidateRucAsync(request.Ruc, cancellationToken)); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (HttpRequestException) { return StatusCode(502, new { message = "No fue posible consultar la información del proveedor en SAP." }); }
    }

    [HttpPost("request-access-key")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<ActionResult<AccessKeyResponse>> RequestAccessKey(RequestAccessKeyRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await registration.RequestAccessKeyAsync(request.Ruc, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (HttpRequestException) { return StatusCode(502, new { message = "No fue posible consultar la información del proveedor en SAP." }); }
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
