namespace WebProveedores.Api.Infrastructure;

/// <summary>Políticas de autorización por rol.</summary>
public static class Policies
{
    public const string UsersManage = "Users.Manage";
    public const string DocumentsRegister = "Documents.Register";
    public const string DocumentsApprove = "Documents.Approve";
    public const string DocumentsAccount = "Documents.Account";
}
