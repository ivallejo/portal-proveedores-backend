using Microsoft.Extensions.Logging.Abstractions;
using WebProveedores.Application.Abstractions.Auth;
using WebProveedores.Application.Abstractions.Documents;
using WebProveedores.Application.Abstractions.Providers;
using WebProveedores.Application.Admin;
using WebProveedores.Application.Auth;
using WebProveedores.Application.Documents;
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

    public static PasswordService Passwords(AppDbContext db, IEmailSender email) =>
        new(new EfUserRepository(db), new EfPasswordTokenRepository(db, TimeProvider.System), new EfUnitOfWork(db), Hasher, Tokens, email, Portal, TimeProvider.System);

    public static ProviderRegistrationService Registration(AppDbContext db, IEmailSender email, IProviderDirectory sap) =>
        new(new EfUserRepository(db), new EfPasswordTokenRepository(db, TimeProvider.System), new EfReferenceDataReader(db), new EfUnitOfWork(db),
            Hasher, email, sap, Portal, TimeProvider.System);

    public static AdminUserService Admin(AppDbContext db) =>
        new(new EfUserRepository(db), new EfReferenceDataReader(db), new EfUnitOfWork(db), Hasher, TimeProvider.System);

    /// <summary>Los cinco servicios de documentos sobre las mismas dependencias.</summary>
    public static DocumentServices Documents(AppDbContext db, IFileStorage storage, IEmailSender email)
    {
        var repository = new EfDocumentRepository(db);
        var access = new DocumentAccess(repository, new EfUserRepository(db));
        var notifier = new DocumentNotifier(email, NullLogger<DocumentNotifier>.Instance);
        var files = new DocumentFiles(storage, new PdfSharpMerger(), NullLogger<DocumentFiles>.Instance);
        var sap = new MockSapDocumentGateway();
        return new DocumentServices(
            new DocumentCatalogService(repository, access, sap),
            new DocumentRegistrationService(repository, access, files, sap, notifier, TimeProvider.System),
            new DocumentQueryService(repository, access, storage),
            new DocumentApprovalService(repository, access, notifier, TimeProvider.System),
            new DocumentAccountingService(repository, access, notifier, TimeProvider.System));
    }
}

internal sealed record DocumentServices(
    IDocumentCatalogService Catalog,
    IDocumentRegistrationService Registration,
    IDocumentQueryService Queries,
    IDocumentApprovalService Approvals,
    IDocumentAccountingService Accounting);
