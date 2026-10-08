using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Api.Contracts.Profile;

public sealed class VerifyEmailRequest
{
    [Required] public string Token { get; init; } = string.Empty;
}
