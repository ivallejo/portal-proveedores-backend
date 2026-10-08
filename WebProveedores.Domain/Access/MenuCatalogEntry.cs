namespace WebProveedores.Domain.Access;

/// <summary>Opción del sistema: código (permiso), ruta, orden inicial, menú padre y roles base que la reciben.</summary>
public sealed record MenuCatalogEntry(string Code, string Name, string? Route, string Icon, int Order, string? Parent, string[] Roles);
