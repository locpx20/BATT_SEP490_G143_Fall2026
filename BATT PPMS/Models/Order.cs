using System;
using System.Collections.Generic;

namespace BATT_PPMS.Models;

/// <summary>Yêu cầu bán hàng. Quantity tính bằng kg (1 bao = 800 kg), UnitPrice là VNĐ/kg.</summary>
public partial class Order
{
    public int OrderId { get; set; }

    public string OrderCode { get; set; } = null!;

    /// <summary>Owner chọn khi duyệt; NULL khi yêu cầu còn chờ duyệt.</summary>
    public int? CustomerId { get; set; }

    /// <summary>Không còn dùng trong luồng mới (tồn kho tính theo lô thành phẩm); giữ cho dữ liệu cũ.</summary>
    public int? InventoryId { get; set; }

    public int Quantity { get; set; }

    /// <summary>Loại thành phẩm cần bán: A hoặc B.</summary>
    public string? Grade { get; set; }

    public decimal? UnitPrice { get; set; }

    public decimal TotalAmount { get; set; }

    public string? Status { get; set; }

    public string? RejectionReason { get; set; }

    public string? Note { get; set; }

    public int? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime OrderDate { get; set; }

    public virtual User? ApprovedByNavigation { get; set; }

    public virtual User? CreatedByNavigation { get; set; }

    public virtual Customer? Customer { get; set; }

    public virtual ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();

    public virtual InventoryItem? Inventory { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}
