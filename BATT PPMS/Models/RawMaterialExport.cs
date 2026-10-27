using System;
using System.Collections.Generic;

namespace BATT_PPMS.Models;

public partial class RawMaterialExport
{
    public int ExportId { get; set; }

    public int MaterialId { get; set; }

    public decimal Quantity { get; set; }

    public DateTime ExportDate { get; set; }

    public int ExportedBy { get; set; }

    public string? Note { get; set; }

    public virtual User ExportedByNavigation { get; set; } = null!;

    public virtual RawMaterial Material { get; set; } = null!;
}
