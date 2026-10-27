using System;
using System.Collections.Generic;

namespace BATT_PPMS.Models;

public partial class MaintenanceRequest
{
    public int RequestId { get; set; }

    public int BrokenReportId { get; set; }

    public int RequestedBy { get; set; }

    public int? AssignedTo { get; set; }

    public DateTime RequestDate { get; set; }

    public string? Status { get; set; }

    public string? Priority { get; set; }

    public string? Description { get; set; }

    public virtual User? AssignedToNavigation { get; set; }

    public virtual BrokenEquipmentReport BrokenReport { get; set; } = null!;

    public virtual ICollection<MaterialPurchaseRequest> MaterialPurchaseRequests { get; set; } = new List<MaterialPurchaseRequest>();

    public virtual User RequestedByNavigation { get; set; } = null!;
}
