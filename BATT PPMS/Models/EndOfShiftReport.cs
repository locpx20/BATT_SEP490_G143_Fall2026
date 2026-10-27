using System;
using System.Collections.Generic;

namespace BATT_PPMS.Models;

public partial class EndOfShiftReport
{
    public int ReportId { get; set; }

    public int AssignmentId { get; set; }

    public int StaffId { get; set; }

    public DateOnly ShiftDate { get; set; }

    public int ProductionQuantity { get; set; }

    public string? Note { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual PersonnelAssignment Assignment { get; set; } = null!;

    public virtual User Staff { get; set; } = null!;
}
