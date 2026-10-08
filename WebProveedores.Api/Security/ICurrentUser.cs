namespace WebProveedores.Api.Security;

/// <summary>Usuario de la petición actual, leído del token. Los controladores no leen claims directamente.</summary>
public interface ICurrentUser
{
    /// <summary>Id del usuario autenticado; si el token no lo trae, la sesión no es válida.</summary>
    Guid Id { get; }

    /// <summary>La sesión se abrió con una contraseña temporal y solo sirve para cambiarla.</summary>
    bool IsPasswordChangeSession { get; }
}
