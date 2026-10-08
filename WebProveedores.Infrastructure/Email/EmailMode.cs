namespace WebProveedores.Infrastructure.Email;

/// <summary>Qué hace el portal con los correos.</summary>
public enum EmailMode
{
    /// <summary>Envío real a cada destinatario (producción).</summary>
    Send,
    /// <summary>Todos los correos van solo a <c>Email:TestRecipient</c> (desarrollo y QA).</summary>
    Redirect,
    /// <summary>No envía nada: deja destinatario, asunto y enlaces en el log.</summary>
    Log,
}
