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

    // Thông tin do nhân viên nhập khi tạo yêu cầu
    public string? Title { get; set; }

    // Vị trí máy (khu vực / vị trí cụ thể trong xưởng)
    public string? Location { get; set; }

    public bool MachineStopped { get; set; }

    // Manager duyệt / từ chối
    public int? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? RejectionReason { get; set; }

    // Phân công cho Maintenance Staff
    public string? AssignmentNote { get; set; }

    public DateTime? AssignedAt { get; set; }

    public DateTime? DueDate { get; set; }

    // Maintenance Staff xác nhận / hoàn thành
    public DateTime? ConfirmedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? CompletionNote { get; set; }

    public virtual User? ReviewedByNavigation { get; set; }

    public virtual ICollection<MaintenanceRequestLog> Logs { get; set; } = new List<MaintenanceRequestLog>();

    public virtual User? AssignedToNavigation { get; set; }

    public virtual BrokenEquipmentReport BrokenReport { get; set; } = null!;

    public virtual ICollection<MaterialPurchaseRequest> MaterialPurchaseRequests { get; set; } = new List<MaterialPurchaseRequest>();

    public virtual User RequestedByNavigation { get; set; } = null!;
}
