namespace WebProveedores.Application.Admin.Responses;

/// <summary>Opciones de los formularios de usuario (incluye inactivos para mostrarlos deshabilitados).</summary>
public sealed record AdminCatalogResponse(IReadOnlyList<AdminRoleOption> Roles, IReadOnlyList<AdminAreaOption> Areas, IReadOnlyList<AdminCompanyOption> Companies);
