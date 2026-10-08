using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Application.Contracts.Auth.Requests;

public sealed class RequestAccessKeyRequest
{
    [Required, RegularExpression(@"^\d{11}$")] public string Ruc { get; init; } = string.Empty;
    [Range(typeof(bool), "true", "true")] public bool TermsAccepted { get; init; }
}
