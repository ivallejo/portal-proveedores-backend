using WebProveedores.Domain.Common;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Organization;

namespace WebProveedores.Tests;

/// <summary>Reglas de registro y de estado que viven en la entidad, sin base de datos.</summary>
public sealed class SupplierDocumentTests
{
    private static readonly Company Naviera = new() { Code = "1001", Name = "Naviera Transoceánica" };
    private static readonly ApproverAssignment Approver = new(Guid.CreateVersion7(), "Finanzas", Guid.CreateVersion7(), "María Torres", "mtorres@test.pe");
    private static readonly PurchaseOrderInfo Order = new(OrderType.Service, "4500012873", 18450m, "Mantenimiento");

    [Fact]
    public void Register_routes_each_entry_type_to_its_first_status()
    {
        Assert.Equal(DocumentStatus.PendingApproval, Register(DocumentEntryType.WithoutPurchaseOrder, approver: Approver).Status);
        Assert.Equal(DocumentStatus.PendingAccounting, Register(DocumentEntryType.WithoutPurchaseOrder, pettyCash: true).Status);
        Assert.Equal(DocumentStatus.PendingAccounting, Register(DocumentEntryType.WithPurchaseOrder, order: Order).Status);

        var special = Register(DocumentEntryType.Special);
        Assert.Equal(DocumentStatus.PendingAccounting, special.Status);
        Assert.Equal("Pendiente de contabilización", special.Events.Single(item => item.Kind == DocumentEventKind.Current).Title);
    }

    [Theory]
    [InlineData(DocumentEntryType.WithPurchaseOrder, false, false, false)]   // Con OC sin orden
    [InlineData(DocumentEntryType.WithoutPurchaseOrder, false, false, false)] // Sin OC sin aprobador
    [InlineData(DocumentEntryType.WithPurchaseOrder, true, true, false)]     // Caja Chica con OC
    [InlineData(DocumentEntryType.WithoutPurchaseOrder, true, false, true)]   // Caja Chica con aprobador
    [InlineData(DocumentEntryType.Special, false, false, true)]               // Especial con aprobador
    public void Register_rejects_inconsistent_combinations(DocumentEntryType entryType, bool pettyCash, bool withOrder, bool withApprover)
    {
        Assert.Throws<DomainRuleException>(() => Register(entryType, pettyCash, withOrder ? Order : null, withApprover ? Approver : null));
    }

    [Fact]
    public void Approved_document_cannot_be_approved_or_reassigned_again()
    {
        var document = Register(DocumentEntryType.WithoutPurchaseOrder, approver: Approver);
        document.Approve(ApprovalReferenceType.Order, "ped-1", "María Torres", DateTime.UtcNow);

        Assert.Equal("PED-1", document.ApprovalReference);
        Assert.Throws<DomainRuleException>(() => document.Approve(ApprovalReferenceType.Order, "PED-2", "María Torres", DateTime.UtcNow));
        Assert.Throws<DomainRuleException>(() => document.Reassign(Approver with { ApproverId = Guid.CreateVersion7() }, "Motivo", "María Torres", DateTime.UtcNow));
        Assert.Single(document.Events, item => item.Kind == DocumentEventKind.Current);
    }

    private static SupplierDocument Register(DocumentEntryType entryType, bool pettyCash = false, PurchaseOrderInfo? order = null, ApproverAssignment? approver = null) =>
        SupplierDocument.Register(
            new DocumentData("F001-1", entryType, "Factura", "20100003199", "Proveedor", null, Currency.PEN, 100m, 18m, 118m, "Servicio", new DateOnly(2026, 10, 1), "Validado"),
            Naviera, Guid.CreateVersion7(), "Usuario (interno)", DateTime.UtcNow, "Validado en SAP", pettyCash, order, approver);
}
