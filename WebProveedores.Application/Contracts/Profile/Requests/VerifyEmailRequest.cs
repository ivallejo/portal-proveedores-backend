using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Application.Contracts.Profile.Requests;

public sealed class VerifyEmailRequest
{
    [Required] public string Token { get; init; } = string.Empty;
}
