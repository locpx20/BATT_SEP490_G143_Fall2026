using System;
using System.Collections.Generic;

namespace BATT_PPMS.Models;

public partial class Order
{
    public int OrderId { get; set; }

    public string OrderCode { get; set; } = null!;

    public int CustomerId { get; set; }

    public int InventoryId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal TotalAmount { get; set; }

    public string? Status { get; set; }

    public string? RejectionReason { get; set; }

    public int? ApprovedBy { get; set; }

    public int CreatedBy { get; set; }

    public DateTime OrderDate { get; set; }

    public virtual User? ApprovedByNavigation { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual Customer Customer { get; set; } = null!;

    public virtual ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();

    public virtual InventoryItem Inventory { get; set; } = null!;

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}
