using System;
using System.Collections.Generic;

namespace BATT_PPMS.Models;

public partial class BrokenEquipmentReport
{
    public int ReportId { get; set; }

    public int MachineId { get; set; }

    public string? Description { get; set; }

    public int ReportedBy { get; set; }

    public DateTime ReportDate { get; set; }

    public string? Status { get; set; }

    public string? Severity { get; set; }

    public virtual Machine Machine { get; set; } = null!;

    public virtual ICollection<MaintenanceRequest> MaintenanceRequests { get; set; } = new List<MaintenanceRequest>();

    public virtual User ReportedByNavigation { get; set; } = null!;
}
