using WebProveedores.Application.Organization.Commands;

namespace WebProveedores.Api.Contracts.Organization;

/// <summary>Traduce los requests HTTP de Organization a los commands de sus casos de uso.</summary>
internal static class OrganizationRequestMappings
{
    public static SaveCompanyCommand ToCommand(this CompanyRequest request) => new() { Code = request.Code, Name = request.Name, Ruc = request.Ruc, BillingEmail = request.BillingEmail };

    public static SaveAreaCommand ToCommand(this AreaRequest request) => new() { CompanyId = request.CompanyId, Name = request.Name, Description = request.Description };
}
