using System;
using System.Collections.Generic;

namespace BATT_PPMS.Models;

public partial class InventoryItem
{
    public int InventoryId { get; set; }

    public string ItemCode { get; set; } = null!;

    public string ItemName { get; set; } = null!;

    public string Unit { get; set; } = null!;

    public int Quantity { get; set; }

    public int MinQuantity { get; set; }

    public string? Status { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
