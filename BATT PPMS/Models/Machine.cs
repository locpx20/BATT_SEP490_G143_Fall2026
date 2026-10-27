using System;
using System.Collections.Generic;

namespace BATT_PPMS.Models;

public partial class Machine
{
    public int MachineId { get; set; }

    public string MachineCode { get; set; } = null!;

    public string MachineName { get; set; } = null!;

    public string ProcessName { get; set; } = null!;

    public string? Brand { get; set; }

    public int CreatedBy { get; set; }

    public virtual ICollection<BrokenEquipmentReport> BrokenEquipmentReports { get; set; } = new List<BrokenEquipmentReport>();

    public virtual User CreatedByNavigation { get; set; } = null!;
}
