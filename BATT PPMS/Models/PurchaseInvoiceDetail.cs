using System;
using System.Collections.Generic;

namespace BATT_PPMS.Models;

public partial class PurchaseInvoiceDetail
{
    public int InvoiceDetailId { get; set; }

    public int InvoiceId { get; set; }

    public int MaterialId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public virtual PurchaseInvoice Invoice { get; set; } = null!;

    public virtual RawMaterial Material { get; set; } = null!;
}
