namespace WebProveedores.Application.Admin.Responses;

/// <summary>Área asignable: sus sociedades por código (los nombres están en el catálogo de sociedades).</summary>
public sealed record AdminAreaOption(Guid Id, string Name, IReadOnlyList<string> CompanyCodes, bool IsActive);
