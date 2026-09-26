using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebProveedores.Application.Auth;

namespace WebProveedores.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService auth) : ControllerBase
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
        return response is null ? Unauthorized(new { message = "Correo o contraseña inválidos." }) : Ok(response);
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<UserResponse> Me() => auth.GetCurrentUser(User) is { } user ? Ok(user) : Unauthorized();
}
