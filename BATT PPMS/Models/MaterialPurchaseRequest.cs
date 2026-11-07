using System;
using System.Collections.Generic;

namespace BATT_PPMS.Models;

public partial class MaterialPurchaseRequest
{
    public int RequestId { get; set; }

    public int MaintenanceRequestId { get; set; }

    public string ItemName { get; set; } = null!;

    public int Quantity { get; set; }

    public int? RequestedBy { get; set; }

    public string? Status { get; set; }

    public DateTime RequestDate { get; set; }

    public int? ApprovedBy { get; set; }

    public string? Unit { get; set; }

    // Ghi chú của nhân viên bảo trì khi gửi yêu cầu
    public string? Note { get; set; }

    // Manager: ghi chú mua hàng hoặc lý do từ chối
    public string? ResultNote { get; set; }

    public decimal? TotalCost { get; set; }

    public DateTime? HandledAt { get; set; }

    public virtual User? ApprovedByNavigation { get; set; }

    public virtual MaintenanceRequest MaintenanceRequest { get; set; } = null!;

    public virtual User? RequestedByNavigation { get; set; }
}
