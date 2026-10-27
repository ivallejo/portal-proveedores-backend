using WebProveedores.Application.Common.Access;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Application.Ports.Outbound.Persistence;
using WebProveedores.Application.Ports.Outbound.Persistence.Models;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Organization;

namespace WebProveedores.Application.Documents;

/// <summary>Carga al actor y aplica quién puede ver o atender cada documento (roles y sociedades).</summary>
internal sealed class DocumentAccess(
    IDocumentRepository documents,
    IApproverDirectory approverDirectory,
    ICompanyReader companyReader,
    PortalActors actors)
{
    public async Task<DocumentActor> LoadActorAsync(Guid userId, CancellationToken cancellationToken)
    {
        var actor = await actors.LoadAsync(userId, cancellationToken);
        return new DocumentActor(actor.Id, actor.Name, actor.Email, actor.Ruc, actor.AreaId, actor.AreaName, actor.Roles, actor.Permissions, actor.CompanyIds);
    }

    /// <summary>Lo propio (registrado, emitido con su RUC o asignado a él) siempre es visible; lo demás, solo en sus sociedades.</summary>
    public async Task<(DocumentActor Actor, SupplierDocument Document)> LoadVisibleAsync(Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        var actor = await LoadActorAsync(userId, cancellationToken);
        var document = await RequireDocumentAsync(documentId, cancellationToken);
        var visible = actor.IsAdmin
            || (actor.IsAccounting && actor.HasCompany(document.CompanyId))
            || (actor.IsApprover && (document.ApproverId == actor.Id || (actor.AreaId is not null && document.AreaId == actor.AreaId && actor.HasCompany(document.CompanyId))))
            || (actor.IsProvider && document.ProviderRuc == actor.Ruc)
            || document.RegisteredById == actor.Id;
        if (!visible) throw new NotFoundException("El documento no existe.");
        return (actor, document);
    }

    /// <summary>Solo el aprobador asignado (o el administrador) atiende la aprobación.</summary>
    public async Task<(DocumentActor Actor, SupplierDocument Document)> LoadForApprovalAsync(Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        var actor = await LoadActorAsync(userId, cancellationToken);
        var document = await RequireDocumentAsync(documentId, cancellationToken);
        if (!actor.IsAdmin && !(actor.IsApprover && document.ApproverId == actor.Id))
            throw new ForbiddenException("Solo el aprobador asignado puede atender este documento.");
        return (actor, document);
    }

    /// <summary>Cuentas por pagar (o el administrador), en las sociedades que tiene asignadas.</summary>
    public async Task<(DocumentActor Actor, SupplierDocument Document)> LoadForAccountingAsync(Guid userId, Guid documentId, string deniedMessage, CancellationToken cancellationToken)
    {
        var actor = await LoadActorAsync(userId, cancellationToken);
        if (!actor.IsAccounting && !actor.IsAdmin) throw new ForbiddenException(deniedMessage);
        var document = await RequireDocumentAsync(documentId, cancellationToken);
        if (!actor.HasCompany(document.CompanyId)) throw new ForbiddenException("No tienes asignada la sociedad de este documento.");
        return (actor, document);
    }

    public async Task<Company> RequireCompanyAsync(DocumentActor actor, string code, CancellationToken cancellationToken)
    {
        var company = await companyReader.FindByCodeAsync(code.Trim(), cancellationToken);
        if (company is not { IsActive: true }) throw new ValidationException("La sociedad seleccionada no es válida.");
        if (!actor.HasCompany(company.Id)) throw new ForbiddenException($"No tienes asignada la sociedad {company.Name}.");
        return company;
    }

    /// <summary>El aprobador elegido debe trabajar con la sociedad del documento.</summary>
    public async Task<ApproverRecord> RequireApproverAsync(Guid approverId, Company company, CancellationToken cancellationToken)
    {
        var approver = await approverDirectory.FindApproverAsync(approverId, cancellationToken)
            ?? throw new ValidationException("El aprobador seleccionado no es válido.");
        if (!approver.CompanyCodes.Contains(company.Code))
            throw new ValidationException($"{approver.Name} no aprueba documentos de la sociedad {company.Name}.");
        return approver;
    }

    private async Task<SupplierDocument> RequireDocumentAsync(Guid documentId, CancellationToken cancellationToken) =>
        await documents.FindAsync(documentId, cancellationToken) ?? throw new NotFoundException("El documento no existe.");
}
