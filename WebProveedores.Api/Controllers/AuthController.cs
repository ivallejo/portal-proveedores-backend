using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebProveedores.Application.Auth;

namespace WebProveedores.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService auth, IOnlineRegistrationService onlineRegistration) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<UserResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await auth.RegisterAsync(request, cancellationToken)); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await auth.LoginAsync(request, cancellationToken);
        return response is null ? Unauthorized(new { message = "RUC, usuario o contraseña inválidos." }) : Ok(response);
    }

    [HttpPost("validate-ruc")]
    [AllowAnonymous]
    public ActionResult<ProviderLookupResponse> ValidateRuc(ValidateRucRequest request)
    {
        try { return Ok(onlineRegistration.ValidateRuc(request.Ruc)); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpPost("request-access-key")]
    [AllowAnonymous]
    public async Task<ActionResult<AccessKeyResponse>> RequestAccessKey(RequestAccessKeyRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await onlineRegistration.RequestAccessKeyAsync(request.Ruc, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpPost("password-reset/request")]
    [AllowAnonymous]
    public async Task<ActionResult<PasswordResetResponse>> RequestPasswordReset(PasswordResetRequest request, CancellationToken cancellationToken)
    {
        var response = await auth.RequestPasswordResetAsync(request, cancellationToken);
        return response is null ? NotFound(new { message = "No encontramos información para el RUC indicado." }) : Ok(response);
    }

    [HttpPost("password-reset/confirm")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmPasswordReset(PasswordResetConfirmRequest request, CancellationToken cancellationToken)
    {
        var confirmed = await auth.ConfirmPasswordResetAsync(request, cancellationToken);
        return confirmed ? NoContent() : BadRequest(new { message = "El enlace de recuperación es inválido o ya venció." });
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<UserResponse> Me() => auth.GetCurrentUser(User) is { } user ? Ok(user) : Unauthorized();
}
