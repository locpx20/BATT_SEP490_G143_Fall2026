using System;
using System.Collections.Generic;

namespace BATT_PPMS.Models;

public partial class Delivery
{
    public int DeliveryId { get; set; }

    public int OrderId { get; set; }

    public string LicensePlate { get; set; } = null!;

    public string DriverName { get; set; } = null!;

    public DateTime PickupDate { get; set; }

    public string? DeliveryStatus { get; set; }

    public int ConfirmedBy { get; set; }

    public virtual User ConfirmedByNavigation { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;
}
