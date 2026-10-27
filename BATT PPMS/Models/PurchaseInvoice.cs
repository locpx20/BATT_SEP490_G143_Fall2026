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

    public int? ApprovedBy { get; set; }

    public string? Note { get; set; }

    public virtual User? ApprovedByNavigation { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<PurchaseInvoiceDetail> PurchaseInvoiceDetails { get; set; } = new List<PurchaseInvoiceDetail>();

    public virtual Supplier Supplier { get; set; } = null!;
}
