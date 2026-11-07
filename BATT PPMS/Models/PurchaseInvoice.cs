using System;
using System.Collections.Generic;

namespace BATT_PPMS.Models;

public partial class PurchaseInvoice
{
    public int InvoiceId { get; set; }

    public string InvoiceCode { get; set; } = null!;

    public int SupplierId { get; set; }

    public DateTime PurchaseDate { get; set; }

    public decimal TotalAmount { get; set; }

    public string? Status { get; set; }

    public int CreatedBy { get; set; }

    // Nhân viên thu mua được phân công (CreatedBy = người tạo phiếu tổng)
    public int? AssignedTo { get; set; }

    public virtual User? AssignedToNavigation { get; set; }

    public virtual ICollection<PurchaseReceipt> PurchaseReceipts { get; set; } = new List<PurchaseReceipt>();

    public string? Note { get; set; }



    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<PurchaseInvoiceDetail> PurchaseInvoiceDetails { get; set; } = new List<PurchaseInvoiceDetail>();

    public virtual Supplier Supplier { get; set; } = null!;
}
