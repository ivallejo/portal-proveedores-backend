using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebProveedores.Api.Contracts.Profile;
using WebProveedores.Api.Security;
using WebProveedores.Application.Profile;
using WebProveedores.Application.Profile.Responses;

namespace WebProveedores.Api.Controllers;

/// <summary>Mi perfil de quien tiene sesión. El cambio de contraseña es <c>api/auth/change-password</c>.</summary>
[ApiController]
[Route("api/profile")]
[Authorize]
public sealed class ProfileController(IProfileService profile, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ProfileResponse>> Get(CancellationToken cancellationToken) =>
        Ok(await profile.GetAsync(currentUser.Id, cancellationToken));

    [HttpPut]
    public async Task<ActionResult<ProfileResponse>> Update(UpdateProfileRequest request, CancellationToken cancellationToken) =>
        Ok(await profile.UpdateAsync(currentUser.Id, request.ToCommand(), cancellationToken));

    [HttpPost("emails")]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<ActionResult<ProfileResponse>> AddEmail(AddEmailRequest request, CancellationToken cancellationToken) =>
        Ok(await profile.AddEmailAsync(currentUser.Id, request.ToCommand(), cancellationToken));

    [HttpPost("emails/{id:guid}/verification")]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<ActionResult<ProfileResponse>> ResendVerification(Guid id, CancellationToken cancellationToken) =>
        Ok(await profile.ResendVerificationAsync(currentUser.Id, id, cancellationToken));

    [HttpPost("emails/{id:guid}/primary")]
    public async Task<ActionResult<ProfileResponse>> MakePrimary(Guid id, CancellationToken cancellationToken) =>
        Ok(await profile.MakePrimaryAsync(currentUser.Id, id, cancellationToken));

    [HttpDelete("emails/{id:guid}")]
    public async Task<ActionResult<ProfileResponse>> RemoveEmail(Guid id, CancellationToken cancellationToken) =>
        Ok(await profile.RemoveEmailAsync(currentUser.Id, id, cancellationToken));

    /// <summary>Enlace del correo de verificación: no requiere sesión (puede abrirse en otro dispositivo).</summary>
    [HttpPost("emails/verify")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<IActionResult> Verify(VerifyEmailRequest request, CancellationToken cancellationToken) =>
        await profile.VerifyEmailAsync(request.Token, cancellationToken) is { } email
            ? Ok(new { email })
            : BadRequest(new { message = "El enlace de verificación es inválido o ya venció." });
}
