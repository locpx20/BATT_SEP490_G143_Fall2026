using System;
using System.Collections.Generic;

namespace BATT_PPMS.Models;

/// <summary>
/// Yêu cầu nhập kho nguyên liệu thô (IR-26xxx) được tạo từ một Phiếu mua hàng đã duyệt (Purchase Order: PM-26xxx).
/// Một Phiếu mua hàng (PurchaseReceipt) có thể chia thành nhiều đợt giao hàng / nhiều Yêu cầu nhập kho (InboundRequest).
/// </summary>
public partial class InboundRequest
{
    public int InboundRequestId { get; set; }

    public string RequestCode { get; set; } = null!;

    public int ReceiptId { get; set; }

    public int InvoiceId { get; set; }

    public int SupplierId { get; set; }

    public decimal DeliveryQuantity { get; set; }

    public decimal UnitPrice { get; set; }

    public string LicensePlate { get; set; } = null!;

    public string DriverName { get; set; } = null!;

    public DateTime DeliveryTime { get; set; }

    public string? ScalePhotoPath { get; set; }

    public string? InvoicePhotoPath { get; set; }

    public string Status { get; set; } = "WaitWarehouse";

    public string? RejectionReason { get; set; }

    public string? Note { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual PurchaseReceipt Receipt { get; set; } = null!;

    public virtual PurchaseInvoice Invoice { get; set; } = null!;

    public virtual Supplier Supplier { get; set; } = null!;

    public virtual User? CreatedByNavigation { get; set; }

    public virtual ICollection<RawMaterialImport> RawMaterialImports { get; set; } = new List<RawMaterialImport>();
}

