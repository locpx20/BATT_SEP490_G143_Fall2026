using System;
using System.Collections.Generic;

namespace BATT_PPMS.Models;

/// <summary>Nhật ký từng bước xử lý của một yêu cầu sửa chữa (chỉ thêm, không sửa/xoá).</summary>
public partial class MaintenanceRequestLog
{
    public int LogId { get; set; }

    public int RequestId { get; set; }

    public string Action { get; set; } = null!;

    public string? FromStatus { get; set; }

    public string? ToStatus { get; set; }

    public int ActorId { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual MaintenanceRequest Request { get; set; } = null!;

    public virtual User Actor { get; set; } = null!;
}
