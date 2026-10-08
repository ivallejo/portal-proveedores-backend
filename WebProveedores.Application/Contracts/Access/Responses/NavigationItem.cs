namespace WebProveedores.Application.Contracts.Access.Responses;

/// <summary>Opción del menú lateral de quien tiene sesión (dos niveles).</summary>
public sealed record NavigationItem(string Code, string Name, string? Route, string Icon, IReadOnlyList<NavigationItem> Children);
