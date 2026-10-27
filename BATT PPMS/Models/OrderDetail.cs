using System;
using System.Collections.Generic;

namespace BATT_PPMS.Models;

public partial class OrderDetail
{
    public int OrderDetailId { get; set; }

    public int OrderId { get; set; }

    public int BatchId { get; set; }

    public decimal Quantity { get; set; }

    public virtual FinishedGoodBatch Batch { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;
}
