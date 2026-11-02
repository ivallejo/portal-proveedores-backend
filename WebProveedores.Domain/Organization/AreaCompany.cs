namespace WebProveedores.Domain.Organization;

/// <summary>Sociedad en la que existe un área: un área puede estar en varias sociedades.</summary>
public sealed class AreaCompany
{
    public Guid AreaId { get; set; }
    public Guid CompanyId { get; set; }

    public Area Area { get; set; } = null!;
    public Company Company { get; set; } = null!;
}
