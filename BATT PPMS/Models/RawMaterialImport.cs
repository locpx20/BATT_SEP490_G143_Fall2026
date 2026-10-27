using System;
using System.Collections.Generic;

namespace BATT_PPMS.Models;

public partial class RawMaterialImport
{
    public int ImportId { get; set; }

    public int MaterialId { get; set; }

    public int SupplierId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public DateTime ImportDate { get; set; }

    public int ImportedBy { get; set; }

    public string? Note { get; set; }

    public virtual User ImportedByNavigation { get; set; } = null!;

    public virtual RawMaterial Material { get; set; } = null!;

    public virtual Supplier Supplier { get; set; } = null!;
}
