using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WebProveedores.Application.Ports.Outbound.Files;
using WebProveedores.Application.Ports.Outbound.Notifications;
using WebProveedores.Application.Ports.Outbound.Persistence;
using WebProveedores.Application.Ports.Outbound.Sap;
using WebProveedores.Application.Ports.Outbound.Security;
using WebProveedores.Infrastructure.Email;
using WebProveedores.Infrastructure.Files;
using WebProveedores.Infrastructure.Persistence;
using WebProveedores.Infrastructure.Persistence.Repositories;
using WebProveedores.Infrastructure.Persistence.Seeding;
using WebProveedores.Infrastructure.Sap;
using WebProveedores.Infrastructure.Security;

namespace WebProveedores.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Adaptadores de los puertos de la aplicación. La configuración se valida aquí, al arrancar.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool isProduction)
    {
        services.AddSingleton(TimeProvider.System);

        // Persistencia (SQL Server).
        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(
            configuration.GetConnectionString("DefaultConnection"), sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<IUserQueries, EfUserQueries>();
        services.AddScoped<IUserUniquenessChecker, EfUserUniquenessChecker>();
        services.AddScoped<IPasswordTokenRepository, EfPasswordTokenRepository>();
        services.AddScoped<IRoleReader, EfRoleReader>();
        services.AddScoped<IPermissionReader, EfPermissionReader>();
        services.AddScoped<IAccessRepository, EfAccessRepository>();
        services.AddScoped<ICompanyReader, EfCompanyReader>();
        services.AddScoped<IOrganizationReader, EfOrganizationReader>();
        services.AddScoped<IOrganizationRepository, EfOrganizationRepository>();
        services.AddScoped<IDocumentRepository, EfDocumentRepository>();
        services.AddScoped<IDocumentSearch, EfDocumentSearch>();
        services.AddScoped<IApproverDirectory, EfApproverDirectory>();
        services.AddScoped<ReferenceDataSeeder>();
        services.AddScoped<DatabaseInitializer>();

        // Sesión y contraseñas.
        services.AddSingleton(JwtSettings.From(configuration));
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();

        // Archivos y PDF.
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IPdfMerger, PdfSharpMerger>();

        // SAP: consulta de proveedores real, con reintentos y corte de circuito; servicios 01/02 según Sap:DocumentServices.
        services.AddSingleton(SapSettings.From(configuration));
        AddSapClient<SapProviderClient>(services);
        services.AddScoped<IProviderDirectory>(provider => provider.GetRequiredService<SapProviderClient>());
        // Pagos y facturas devuelven más datos: más tiempo por intento.
        AddSapClient<SapPaymentsClient>(services, attemptSeconds: 25, totalSeconds: 60);
        services.AddScoped<ISapPaymentsGateway>(provider => provider.GetRequiredService<SapPaymentsClient>());
        var sapDocuments = SapDocumentSettings.Resolve(configuration, isProduction);
        services.AddSingleton(sapDocuments);
        services.AddSingleton<ISapDocumentGateway, MockSapDocumentGateway>();

        // Correo (Email:Mode): Send en producción, Redirect al buzón de pruebas fuera de ella.
        var email = EmailSettings.Resolve(configuration, isProduction);
        services.AddSingleton(email);
        services.AddScoped<SmtpEmailSender>();
        services.AddScoped<IEmailSender>(provider => email.Mode switch
        {
            EmailMode.Send => provider.GetRequiredService<SmtpEmailSender>(),
            EmailMode.Redirect => new RedirectingEmailSender(provider.GetRequiredService<SmtpEmailSender>(), email.TestRecipient!),
            _ => ActivatorUtilities.CreateInstance<LogEmailSender>(provider),
        });
        return services;
    }

    /// <summary>Cliente HTTP de SAP con reintentos, tiempos máximos y corte de circuito ante caídas.</summary>
    private static void AddSapClient<TClient>(IServiceCollection services, int attemptSeconds = 10, int totalSeconds = 30) where TClient : class =>
        services.AddHttpClient<TClient>(client => client.Timeout = TimeSpan.FromSeconds(totalSeconds + 5))
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = 2;
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(attemptSeconds);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(totalSeconds);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(Math.Max(20, attemptSeconds * 2));
            });
}
