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

    /// <summary>Khu vực đặt máy (vd: Xưởng 1 – khu băm nghiền). Báo cáo sự cố tự lấy giá trị này.</summary>
    public string? Area { get; set; }

    public string Status { get; set; } = "Active";

    public int? CreatedBy { get; set; }

    public virtual ICollection<BrokenEquipmentReport> BrokenEquipmentReports { get; set; } = new List<BrokenEquipmentReport>();

    public virtual User? CreatedByNavigation { get; set; }
}
