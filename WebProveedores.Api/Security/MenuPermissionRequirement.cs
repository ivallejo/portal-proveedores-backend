using Microsoft.AspNetCore.Authorization;

namespace WebProveedores.Api.Security;

/// <summary>Basta con tener una de las opciones.</summary>
public sealed record MenuPermissionRequirement(IReadOnlyList<string> Codes) : IAuthorizationRequirement;
