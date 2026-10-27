using System;
using System.Collections.Generic;

namespace BATT_PPMS.Models;

public partial class FinishedGoodBatch
{
    public int BatchId { get; set; }

    public string BagNumber { get; set; } = null!;

    public DateOnly ProductionDate { get; set; }

    public decimal Quantity { get; set; }

    public string? Status { get; set; }

    public int CreatedBy { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual ICollection<Qcinspection> Qcinspections { get; set; } = new List<Qcinspection>();
}
