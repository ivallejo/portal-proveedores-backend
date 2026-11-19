using WebProveedores.Domain.Common;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Organization;

namespace WebProveedores.Tests;

/// <summary>Reglas de registro y de estado que viven en la entidad, sin base de datos.</summary>
public sealed class SupplierDocumentTests
{
    private static readonly Company Naviera = new() { Code = "1001", Name = "Naviera Transoceánica" };
    private static readonly ApproverAssignment Approver = new(Guid.CreateVersion7(), "Finanzas", Guid.CreateVersion7(), "María Torres", "mtorres@test.pe");
    private static readonly PurchaseOrderInfo Order = new(OrderType.Service, "4500012873", 18450m, "Mantenimiento", DeliveryType.OnBoard);

    [Fact]
    public void Delivery_type_applies_only_to_service_orders()
    {
        Assert.Null(OrderDelivery.Resolve(OrderType.Goods, null));
        Assert.Throws<DomainRuleException>(() => OrderDelivery.Resolve(OrderType.Goods, DeliveryType.Warehouse));
        Assert.Equal(DeliveryType.OnBoard, OrderDelivery.Resolve(OrderType.Service, null));
        Assert.Equal(DeliveryType.Warehouse, OrderDelivery.Resolve(OrderType.Service, DeliveryType.Warehouse));

        Assert.True(OrderDelivery.IsCarrier(OrderType.Goods, null));
        Assert.True(OrderDelivery.IsCarrier(OrderType.Service, DeliveryType.Warehouse));
        Assert.False(OrderDelivery.IsCarrier(OrderType.Service, DeliveryType.OnBoard));
    }

    [Fact]
    public void Register_keeps_the_delivery_type_and_rejects_inconsistent_orders()
    {
        var warehouse = Register(DocumentEntryType.WithPurchaseOrder,
            order: new PurchaseOrderInfo(OrderType.Service, "CR-2026-00527", 9860m, "Almacenaje", DeliveryType.Warehouse));
        Assert.Equal(DeliveryType.Warehouse, warehouse.DeliveryType);
        Assert.Null(Register(DocumentEntryType.WithPurchaseOrder, order: new PurchaseOrderInfo(OrderType.Goods, "CR-2026-00418", 1m, "Repuestos")).DeliveryType);

        Assert.Throws<DomainRuleException>(() => Register(DocumentEntryType.WithPurchaseOrder,
            order: new PurchaseOrderInfo(OrderType.Service, "4500012873", 1m, "Sin entrega")));
        Assert.Throws<DomainRuleException>(() => Register(DocumentEntryType.WithPurchaseOrder,
            order: new PurchaseOrderInfo(OrderType.Goods, "CR-2026-00418", 1m, "Repuestos", DeliveryType.OnBoard)));
    }

    [Fact]
    public void Register_routes_each_entry_type_to_its_first_status()
    {
        Assert.Equal(DocumentStatus.PendingApproval, Register(DocumentEntryType.WithoutPurchaseOrder, approver: Approver).Status);
        // Sin OC sin aprobador: lo registra un usuario interno (flujo 02) y va directo a contabilización.
        Assert.Equal(DocumentStatus.PendingAccounting, Register(DocumentEntryType.WithoutPurchaseOrder).Status);
        Assert.Equal(DocumentStatus.PendingAccounting, Register(DocumentEntryType.WithPurchaseOrder, order: Order).Status);

        var special = Register(DocumentEntryType.Special);
        Assert.Equal(DocumentStatus.PendingAccounting, special.Status);
        Assert.Equal("Pendiente de contabilización", special.Events.Single(item => item.Kind == DocumentEventKind.Current).Title);
    }

    [Theory]
    [InlineData(DocumentEntryType.WithPurchaseOrder, false, false)]   // Con OC sin orden
    [InlineData(DocumentEntryType.WithPurchaseOrder, true, true)]     // Con OC con aprobador
    [InlineData(DocumentEntryType.WithoutPurchaseOrder, true, true)]  // Sin OC con orden
    [InlineData(DocumentEntryType.Special, false, true)]              // Especial con aprobador
    public void Register_rejects_inconsistent_combinations(DocumentEntryType entryType, bool withOrder, bool withApprover)
    {
        Assert.Throws<DomainRuleException>(() => Register(entryType, withOrder ? Order : null, withApprover ? Approver : null));
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

    private static SupplierDocument Register(DocumentEntryType entryType, PurchaseOrderInfo? order = null, ApproverAssignment? approver = null) =>
        SupplierDocument.Register(
            new DocumentData("F001-1", entryType, "Factura", "20100003199", "Proveedor", null, Currency.PEN, 100m, 18m, 118m, "Servicio", new DateOnly(2026, 10, 1), "Validado"),
            Naviera, Guid.CreateVersion7(), "Usuario (interno)", DateTime.UtcNow, "Validado en SAP", order, approver);
}
