using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Abstractions.Documents;

public interface IDocumentRepository
{
    Task<Company?> FindCompanyAsync(string code, CancellationToken cancellationToken);
    Task<IReadOnlyList<Company>> ListCompaniesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ApproverRecord>> ListApproversAsync(CancellationToken cancellationToken);
    Task<ApproverRecord?> FindApproverAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string providerRuc, string number, CancellationToken cancellationToken);
    Task<SupplierDocument?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<DocumentPage> SearchAsync(DocumentQuery query, CancellationToken cancellationToken);
    void Add(SupplierDocument document);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Usuario activo con rol de aprobador y área asignada.</summary>
public sealed record ApproverRecord(Guid UserId, string Name, string Email, Guid AreaId, string AreaName);

public enum DocumentInbox
{
    /// <summary>Documentos que pasaron o están en aprobación.</summary>
    Approvals,
    /// <summary>Documentos que llegaron a Cuentas por pagar.</summary>
    Accounting,
    /// <summary>Documentos registrados por el usuario o emitidos con su RUC.</summary>
    Mine,
}

public sealed record DocumentQuery(
    DocumentInbox Inbox,
    string? ProviderRuc,
    DocumentStatus? Status,
    int Page,
    int PageSize,
    // Si se indica, limita la bandeja de aprobación a ese aprobador o a su área.
    Guid? ApproverId = null,
    Guid? ApproverAreaId = null,
    // Si se indica, limita «Mine» a ese RUC emisor.
    string? OwnerRuc = null,
    // Si se indica, limita «Mine» a lo registrado por ese usuario.
    Guid? RegisteredById = null);

public sealed record DocumentPage(
    IReadOnlyList<SupplierDocument> Items,
    int Total,
    IReadOnlyDictionary<DocumentStatus, int> CountsByStatus);

/// <summary>Almacenamiento de los archivos adjuntos.</summary>
public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}

/// <summary>Servicios SAP del flujo documental (01: orden de compra, 02: SUNAT y duplicidad).</summary>
public interface ISapDocumentGateway
{
    Task<SapOrder?> ValidateOrderAsync(string companyCode, OrderType type, string number, CancellationToken cancellationToken);
    Task<SapValidationResult> ValidateDocumentAsync(SapDocumentValidation request, CancellationToken cancellationToken);
}

public sealed record SapOrder(string Number, OrderType Type, string Description, decimal Balance);

public sealed record SapDocumentValidation(
    string CompanyCode,
    string ProviderRuc,
    DateOnly IssuedAt,
    string Number,
    decimal Amount,
    bool CheckSunat);

public sealed record SapValidationResult(bool IsValid, string? Message)
{
    public static SapValidationResult Valid { get; } = new(true, null);
}

/// <summary>Une varios PDF en uno solo, en el orden recibido.</summary>
public interface IPdfMerger
{
    /// <summary>Lanza <see cref="InvalidDataException"/> si algún archivo no es un PDF legible.</summary>
    byte[] Merge(IReadOnlyList<byte[]> documents);
}
