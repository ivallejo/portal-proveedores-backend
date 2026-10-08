using Microsoft.Extensions.DependencyInjection;
using WebProveedores.Application.Common.Settings;
using WebProveedores.Application.Ports.Inbound.Access;
using WebProveedores.Application.Ports.Inbound.Admin;
using WebProveedores.Application.Ports.Inbound.Auth;
using WebProveedores.Application.Ports.Inbound.Documents;
using WebProveedores.Application.Ports.Inbound.Organization;
using WebProveedores.Application.Ports.Inbound.Payments;
using WebProveedores.Application.Ports.Inbound.Profile;
using WebProveedores.Application.UseCases.Access;
using WebProveedores.Application.UseCases.Admin;
using WebProveedores.Application.UseCases.Auth;
using WebProveedores.Application.UseCases.Documents;
using WebProveedores.Application.UseCases.Organization;
using WebProveedores.Application.UseCases.Payments;
using WebProveedores.Application.UseCases.Profile;

namespace WebProveedores.Application;

public static class DependencyInjection
{
    /// <summary>Casos de uso. Los adaptadores (persistencia, SAP, correo, tokens) los registra <c>AddInfrastructure</c>.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services, PortalSettings portal, LoginLockoutSettings lockout)
    {
        services.AddSingleton(portal);
        services.AddSingleton(lockout);

        services.AddScoped<PasswordLinks>();
        services.AddScoped<ILoginService, LoginService>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<IProviderRegistrationService, ProviderRegistrationService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<INavigationService, NavigationService>();
        services.AddScoped<IAccessAdminService, AccessAdminService>();
        services.AddScoped<EmailVerifications>();
        services.AddScoped<IProfileService, ProfileService>();

        services.AddScoped<DocumentAccess>();
        services.AddScoped<DocumentFiles>();
        services.AddScoped<DocumentNotifier>();
        services.AddScoped<IDocumentCatalogService, DocumentCatalogService>();
        services.AddScoped<IDocumentRegistrationService, DocumentRegistrationService>();
        services.AddScoped<IDocumentQueryService, DocumentQueryService>();
        services.AddScoped<IDocumentApprovalService, DocumentApprovalService>();
        services.AddScoped<IDocumentAccountingService, DocumentAccountingService>();
        services.AddScoped<IPaymentQueryService, PaymentQueryService>();
        return services;
    }
}
