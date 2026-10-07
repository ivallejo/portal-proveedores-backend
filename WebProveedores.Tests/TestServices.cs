using Microsoft.Extensions.Logging.Abstractions;
using WebProveedores.Application.Admin;
using WebProveedores.Application.Auth;
using WebProveedores.Application.Documents;
using WebProveedores.Application.Ports.Outbound.Files;
using WebProveedores.Application.Ports.Outbound.Notifications;
using WebProveedores.Application.Ports.Outbound.Sap;
using WebProveedores.Application.Ports.Outbound.Security;
using WebProveedores.Application.Profile;
using WebProveedores.Domain.Access;
using WebProveedores.Infrastructure.Auth;
using WebProveedores.Infrastructure.Documents;
using WebProveedores.Infrastructure.Persistence;

namespace WebProveedores.Tests;

/// <summary>Arma los casos de uso con los adaptadores reales (EF InMemory, hasher y JWT) para las pruebas.</summary>
internal static class TestServices
{
    public static readonly IPasswordHasher Hasher = new IdentityPasswordHasher();
    public static readonly ITokenIssuer Tokens = new JwtTokenIssuer(
        new JwtSettings("test-signing-key-with-at-least-32-characters", "test-issuer", "test-audience", 30), TimeProvider.System);
    public static readonly PortalSettings Portal = new("http://localhost:4200");

    public static LoginService Login(AppDbContext db, TimeProvider? clock = null) =>
        new(new EfUserRepository(db), new EfUnitOfWork(db), Hasher, Tokens, new LoginLockoutSettings(5, 15), clock ?? TimeProvider.System);

    public static PasswordLinks Links(AppDbContext db, IEmailSender email) =>
        new(new EfPasswordTokenRepository(db, TimeProvider.System), new EfUnitOfWork(db), email, Portal, TimeProvider.System);

    public static PasswordService Passwords(AppDbContext db, IEmailSender email) =>
        new(new EfUserRepository(db), new EfPasswordTokenRepository(db, TimeProvider.System), new EfUnitOfWork(db), Hasher, Tokens, Links(db, email), TimeProvider.System);

    public static ProviderRegistrationService Registration(AppDbContext db, IEmailSender email, IProviderDirectory sap) =>
        new(new EfUserRepository(db), new EfReferenceDataReader(db), new EfUnitOfWork(db), Hasher, Links(db, email), sap, TimeProvider.System);

    public static IAdminUserService Admin(AppDbContext db, IEmailSender email) =>
        new AdminUserService(new EfUserRepository(db), new EfReferenceDataReader(db), new EfOrganizationRepository(db),
            new EfPasswordTokenRepository(db, TimeProvider.System), new EfUnitOfWork(db), Hasher, Links(db, email),
            new EmailVerifications(email, Portal, TimeProvider.System), TimeProvider.System);

    /// <summary>Los cinco servicios de documentos sobre las mismas dependencias.</summary>
    public static DocumentServices Documents(AppDbContext db, IFileStorage storage, IEmailSender email)
    {
        var repository = new EfDocumentRepository(db);
        var access = new DocumentAccess(repository, new EfUserRepository(db), new EfAccessRepository(db));
        var notifier = new DocumentNotifier(email, NullLogger<DocumentNotifier>.Instance);
        var files = new DocumentFiles(storage, new PdfSharpMerger(), TimeProvider.System, NullLogger<DocumentFiles>.Instance);
        var sap = new MockSapDocumentGateway();
        return new DocumentServices(
            new DocumentCatalogService(repository, access, sap),
            new DocumentRegistrationService(repository, access, files, sap, notifier, TimeProvider.System),
            new DocumentQueryService(repository, access, storage),
            new DocumentApprovalService(repository, access, notifier, TimeProvider.System),
            new DocumentAccountingService(repository, access, notifier, TimeProvider.System));
    }
}

/// <summary>Opciones de menú del sistema con sus roles base, como las crea el arranque (los permisos salen de ellas).</summary>
internal static class TestMenus
{
    public static void Seed(AppDbContext db, IReadOnlyDictionary<string, Role> roles)
    {
        var menus = new Dictionary<string, MenuOption>();
        foreach (var entry in MenuCatalog.System)
        {
            var menu = new MenuOption { Code = entry.Code, Name = entry.Name, Route = entry.Route, Icon = entry.Icon, Order = entry.Order, Parent = entry.Parent is null ? null : menus[entry.Parent] };
            foreach (var code in entry.Roles.Where(roles.ContainsKey)) menu.RoleMenus.Add(new RoleMenu { Role = roles[code], MenuOption = menu });
            menus[entry.Code] = menu;
            db.MenuOptions.Add(menu);
        }
    }
}

internal sealed record DocumentServices(
    IDocumentCatalogService Catalog,
    IDocumentRegistrationService Registration,
    IDocumentQueryService Queries,
    IDocumentApprovalService Approvals,
    IDocumentAccountingService Accounting);
