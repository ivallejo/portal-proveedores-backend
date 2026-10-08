using WebProveedores.Application.Profile.Commands;

namespace WebProveedores.Api.Contracts.Profile;

/// <summary>Traduce los requests HTTP de Profile a los commands de sus casos de uso.</summary>
internal static class ProfileRequestMappings
{
    public static UpdateProfileCommand ToCommand(this UpdateProfileRequest request) => new() { BusinessName = request.BusinessName, FirstName = request.FirstName, LastName = request.LastName };

    public static AddEmailCommand ToCommand(this AddEmailRequest request) => new() { Email = request.Email, Type = request.Type };
}
