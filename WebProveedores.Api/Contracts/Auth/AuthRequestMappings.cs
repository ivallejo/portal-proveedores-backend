using WebProveedores.Application.Contracts.Auth.Commands;

namespace WebProveedores.Api.Contracts.Auth;

/// <summary>Traduce los requests HTTP de Auth a los commands de sus casos de uso.</summary>
internal static class AuthRequestMappings
{
    public static LoginCommand ToCommand(this LoginRequest request) => new() { Identifier = request.Identifier, Password = request.Password };

    public static ChangePasswordCommand ToCommand(this ChangePasswordRequest request) => new() { CurrentPassword = request.CurrentPassword, NewPassword = request.NewPassword };

    public static RequestPasswordResetCommand ToCommand(this PasswordResetRequest request) => new() { Ruc = request.Ruc };

    public static ConfirmPasswordResetCommand ToCommand(this PasswordResetConfirmRequest request) => new() { Ruc = request.Ruc, User = request.User, Token = request.Token, NewPassword = request.NewPassword };

    public static RegisterProviderCommand ToCommand(this RegisterRequest request) => new() { Ruc = request.Ruc, CompanyName = request.CompanyName, Email = request.Email, Password = request.Password };
}
