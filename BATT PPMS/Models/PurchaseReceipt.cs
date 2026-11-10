using System;
using System.Collections.Generic;

namespace BATT_PPMS.Models;

/// <summary>Phiếu mua hàng con (PM-xxxxx) do nhân viên thu mua nhập bên trong một phiếu tổng.</summary>
public partial class PurchaseReceipt
{
    public int ReceiptId { get; set; }

    public string ReceiptCode { get; set; } = null!;

    public int InvoiceId { get; set; }

    public int SupplierId { get; set; }

    public decimal WeighedQuantity { get; set; }

    public decimal ActualPrice { get; set; }

    public string ScalePhotoPath { get; set; } = null!;

    public string? InvoicePhotoPath { get; set; }

    public string Status { get; set; } = "Pending";

    public string? RejectionReason { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual PurchaseInvoice Invoice { get; set; } = null!;

    public virtual Supplier Supplier { get; set; } = null!;

    public virtual User? CreatedByNavigation { get; set; }
}

