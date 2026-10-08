using WebProveedores.Application.Contracts.Admin.Commands;

namespace WebProveedores.Api.Contracts.Admin;

/// <summary>Traduce los requests HTTP de Admin a los commands de sus casos de uso.</summary>
internal static class AdminRequestMappings
{
    public static SaveUserCommand ToCommand(this SaveUserRequest request) => new() { Role = request.Role, Document = request.Document, BusinessName = request.BusinessName, FirstName = request.FirstName, LastName = request.LastName, AreaId = request.AreaId, CompanyCodes = request.CompanyCodes, Emails = request.Emails.Select(email => email.ToData()).ToArray(), Status = request.Status, MustChangePassword = request.MustChangePassword };

    public static SetUserStatusCommand ToCommand(this UpdateUserStatusRequest request) => new(request.IsActive);

    public static UserEmailData ToData(this UserEmailInput request) => new() { Id = request.Id, Email = request.Email, Type = request.Type, IsPrimary = request.IsPrimary };
}
