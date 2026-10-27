using System;
using System.Collections.Generic;

namespace BATT_PPMS.Models;

public partial class Qcinspection
{
    public int InspectionId { get; set; }

    public string InspectionCode { get; set; } = null!;

    public int BatchId { get; set; }

    public DateTime RequestDate { get; set; }

    public string? Result { get; set; }

    public string? Note { get; set; }

    public string? Status { get; set; }

    public int RequestedBy { get; set; }

    public int? InspectedBy { get; set; }

    public DateTime? InspectDate { get; set; }

    public virtual FinishedGoodBatch Batch { get; set; } = null!;

    public virtual User? InspectedByNavigation { get; set; }

    public virtual User RequestedByNavigation { get; set; } = null!;
}
