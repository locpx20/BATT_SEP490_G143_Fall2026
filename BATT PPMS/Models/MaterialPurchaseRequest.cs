using System;
using System.Collections.Generic;

namespace BATT_PPMS.Models;

public partial class MaterialPurchaseRequest
{
    public int RequestId { get; set; }

    public int MaintenanceRequestId { get; set; }

    public string ItemName { get; set; } = null!;

    public int Quantity { get; set; }

    public int RequestedBy { get; set; }

    public string? Status { get; set; }

    public DateTime RequestDate { get; set; }

    public int? ApprovedBy { get; set; }

    public virtual User? ApprovedByNavigation { get; set; }

    public virtual MaintenanceRequest MaintenanceRequest { get; set; } = null!;

    public virtual User RequestedByNavigation { get; set; } = null!;
}
