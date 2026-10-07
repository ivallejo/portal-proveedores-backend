namespace WebProveedores.Domain.Access;

public static class SecurityCatalog
{
    public const string ProviderRole = "PROVIDER";
    public const string InternalUserRole = "INTERNAL_USER";
    public const string AreaApproverRole = "AREA_APPROVER";
    public const string AccountsPayableRole = "ACCOUNTS_PAYABLE";
    public const string AdministratorRole = "ADMINISTRATOR";

    public static readonly IReadOnlyDictionary<string, string> Roles = new Dictionary<string, string>
    {
        [ProviderRole] = "Proveedor",
        [InternalUserRole] = "Usuario interno",
        [AreaApproverRole] = "Aprobador de área",
        [AccountsPayableRole] = "Gestor de cuentas por pagar",
        [AdministratorRole] = "Administrador",
    };

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [ProviderRole] = "Consulta órdenes, pagos y facturas; registra documentos.",
        [InternalUserRole] = "Registra documentos sin orden de compra y documentos especiales.",
        [AreaApproverRole] = "Revisa y aprueba documentos sin orden de compra.",
        [AccountsPayableRole] = "Contabiliza, observa o rechaza documentos aprobados.",
        [AdministratorRole] = "Acceso total, incluida la configuración del portal.",
    };
}
