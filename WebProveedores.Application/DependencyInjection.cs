using Microsoft.Extensions.DependencyInjection;
using WebProveedores.Application.Access;
using WebProveedores.Application.Admin;
using WebProveedores.Application.Auth;
using WebProveedores.Application.Common.Settings;
using WebProveedores.Application.Documents;
using WebProveedores.Application.Organization;
using WebProveedores.Application.Payments;
using WebProveedores.Application.Profile;

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
        services.AddScoped<IPermissionService, PermissionService>();
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
