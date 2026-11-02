using System;
using System.Collections.Generic;

namespace BATT_PPMS.Models;

public partial class RawMaterial
{
    public int MaterialId { get; set; }

    public string MaterialName { get; set; } = null!;

    public string Unit { get; set; } = null!;

    public decimal CurrentQuantity { get; set; }

    // Giá thu mua hiện hành (đ/đơn vị). null = chưa thiết lập -> chưa thể tạo đơn mua.
    public decimal? PurchasePrice { get; set; }

    public virtual ICollection<RawMaterialPriceHistory> PriceHistories { get; set; } = new List<RawMaterialPriceHistory>();

    public virtual ICollection<PurchaseInvoiceDetail> PurchaseInvoiceDetails { get; set; } = new List<PurchaseInvoiceDetail>();

    public virtual ICollection<RawMaterialExport> RawMaterialExports { get; set; } = new List<RawMaterialExport>();

    public virtual ICollection<RawMaterialImport> RawMaterialImports { get; set; } = new List<RawMaterialImport>();
}
