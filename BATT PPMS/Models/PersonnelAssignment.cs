using System;
using System.Collections.Generic;

namespace BATT_PPMS.Models;

public partial class PersonnelAssignment
{
    public int AssignmentId { get; set; }

    public int StaffId { get; set; }

    public int AssignedBy { get; set; }

    public DateOnly AssignmentDate { get; set; }

    public string? Shift { get; set; }

    public string? TaskDescription { get; set; }

    public string? Status { get; set; }

    public virtual User AssignedByNavigation { get; set; } = null!;

    public virtual ICollection<EndOfShiftReport> EndOfShiftReports { get; set; } = new List<EndOfShiftReport>();

    public virtual User Staff { get; set; } = null!;
}
