namespace BATT_PPMS.Helpers;

/// <summary>
/// Luồng yêu cầu sửa chữa máy móc:
/// Pending (Staff tạo) -> Rejected (Manager từ chối, trả Staff) hoặc Assigned (Manager duyệt + phân công)
/// -> InProgress (Maintenance Staff xác nhận) -> Completed. Staff có thể huỷ khi Pending / Rejected.
/// Trong InProgress, Maintenance Staff tự kiểm tra vật tư: đủ thì sửa và hoàn thành; thiếu thì gửi yêu cầu mua
/// cho Manager và chuyển sang WaitingMaterial. Manager mua xong -> thông báo -> quay lại InProgress.
/// </summary>
public static class RepairStatus
{
    public const string Pending = "Pending";
    public const string Rejected = "Rejected";
    public const string Assigned = "Assigned";
    public const string InProgress = "InProgress";
    public const string WaitingMaterial = "WaitingMaterial";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    /// <summary>Mức độ ưu tiên / nghiêm trọng được chọn khi tạo và duyệt yêu cầu.</summary>
    public static readonly string[] PriorityLevels = { "Low", "Medium", "High", "Critical" };

    /// <summary>Chuẩn hoá giá trị cũ trong DB ("Reported" = Pending, "In Progress" = InProgress).</summary>
    public static string Normalize(string? status) => status switch
    {
        null or "" => Pending,
        "Reported" => Pending,
        "In Progress" => InProgress,
        _ => status
    };

    /// <summary>Các giá trị DB tương đương với một trạng thái chuẩn (để lọc truy vấn).</summary>
    public static string[] Equivalents(string normalized) => normalized switch
    {
        Pending => new[] { "Pending", "Reported" },
        InProgress => new[] { "InProgress", "In Progress" },
        _ => new[] { normalized }
    };

    public static string Label(string? status) => Normalize(status) switch
    {
        Pending => "Chờ duyệt",
        Rejected => "Bị từ chối – chờ chỉnh sửa",
        Assigned => "Đã phân công – chờ xác nhận",
        InProgress => "Đang sửa chữa",
        WaitingMaterial => "Chờ vật tư",
        Completed => "Đã hoàn thành",
        Cancelled => "Đã hủy",
        var other => other
    };

    /// <summary>Màu badge (nền, chữ, viền).</summary>
    public static (string Bg, string Fg, string Border) Badge(string? status) => Normalize(status) switch
    {
        Pending => ("#f6ecd2", "#8a6414", "#e8d7a6"),
        Rejected => ("#f1e2da", "#94473e", "#e4c8bf"),
        Assigned => ("#dfe9f3", "#2f5a85", "#bfd2e5"),
        InProgress => ("#e3e7f6", "#3f4a94", "#c6cdea"),
        WaitingMaterial => ("#efe3f3", "#6b3f7a", "#d9c4e0"),
        Completed => ("#e2eee4", "#356e51", "#c6dbc9"),
        Cancelled => ("#ececea", "#6b7570", "#d6d9d4"),
        _ => ("#efefe7", "#45594f", "#e1e5da")
    };

    public static string PriorityLabel(string? priority) => priority switch
    {
        "Low" => "Thấp",
        "Normal" => "Bình thường",
        "Medium" => "Trung bình",
        "High" => "Cao",
        "Critical" => "Khẩn cấp",
        null or "" => "—",
        _ => priority
    };

    public static (string Bg, string Fg, string Border) PriorityBadge(string? priority) => priority switch
    {
        "Critical" => ("#f1d9d4", "#8e2f24", "#e2b9b1"),
        "High" => ("#f4e3d0", "#9a5a1c", "#e6ccab"),
        "Medium" => ("#f6ecd2", "#8a6414", "#e8d7a6"),
        _ => ("#e8eee6", "#4a6a55", "#cfdccf")
    };
}

/// <summary>Các hành động ghi trong nhật ký xử lý yêu cầu.</summary>
public static class RepairAction
{
    public const string Created = "Created";
    public const string Resubmitted = "Resubmitted";
    public const string Rejected = "Rejected";
    public const string Approved = "Approved";
    public const string Reassigned = "Reassigned";
    public const string Confirmed = "Confirmed";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string PurchaseRequested = "PurchaseRequested";
    public const string MaterialsPurchased = "MaterialsPurchased";
    public const string PurchaseRejected = "PurchaseRejected";
    public const string MaterialsReady = "MaterialsReady";

    public static string Label(string? action) => action switch
    {
        Created => "Tạo yêu cầu sửa chữa",
        Resubmitted => "Chỉnh sửa và gửi lại",
        Rejected => "Từ chối – trả về nhân viên",
        Approved => "Phê duyệt và phân công",
        Reassigned => "Phân công lại",
        Confirmed => "Xác nhận nhận việc",
        Completed => "Hoàn thành sửa chữa",
        Cancelled => "Hủy yêu cầu",
        PurchaseRequested => "Thiếu vật tư – gửi yêu cầu mua",
        MaterialsPurchased => "Quản lý đã mua vật tư",
        PurchaseRejected => "Quản lý từ chối mua vật tư",
        MaterialsReady => "Hết chờ vật tư – tiếp tục sửa chữa",
        _ => action ?? "—"
    };

    public static string Icon(string? action) => action switch
    {
        Created => "bi-plus-circle-fill",
        Resubmitted => "bi-arrow-repeat",
        Rejected => "bi-x-circle-fill",
        Approved => "bi-person-check-fill",
        Reassigned => "bi-person-lines-fill",
        Confirmed => "bi-hand-thumbs-up-fill",
        Completed => "bi-check-circle-fill",
        Cancelled => "bi-slash-circle-fill",
        PurchaseRequested => "bi-cart-plus-fill",
        MaterialsPurchased => "bi-bag-check-fill",
        PurchaseRejected => "bi-cart-x-fill",
        MaterialsReady => "bi-play-circle-fill",
        _ => "bi-circle-fill"
    };

    public static string Color(string? action) => action switch
    {
        Rejected => "#94473e",
        Cancelled => "#6b7570",
        Completed => "#356e51",
        Approved or Reassigned => "#2f5a85",
        Confirmed => "#3f4a94",
        MaterialsPurchased or MaterialsReady => "#356e51",
        PurchaseRequested => "#6b3f7a",
        PurchaseRejected => "#94473e",
        _ => "#8a6414"
    };
}


/// <summary>Trạng thái yêu cầu mua vật tư sửa chữa (do Maintenance Staff gửi, Manager xử lý).</summary>
public static class PurchaseRequestStatus
{
    public const string Pending = "Pending";
    public const string Purchased = "Purchased";
    public const string Rejected = "Rejected";

    /// <summary>Dữ liệu cũ dùng "Approved" cho yêu cầu đã được duyệt: coi như đã mua.</summary>
    public static string Normalize(string? status) => status switch
    {
        null or "" => Pending,
        "Approved" => Purchased,
        _ => status
    };

    public static string[] Equivalents(string normalized) => normalized switch
    {
        Purchased => new[] { "Purchased", "Approved" },
        Pending => new[] { "Pending" },
        _ => new[] { normalized }
    };

    public static string Label(string? status) => Normalize(status) switch
    {
        Pending => "Chờ Quản lý mua",
        Purchased => "Đã mua",
        Rejected => "Bị từ chối",
        var other => other
    };

    public static (string Bg, string Fg, string Border) Badge(string? status) => Normalize(status) switch
    {
        Pending => ("#f6ecd2", "#8a6414", "#e8d7a6"),
        Purchased => ("#e2eee4", "#356e51", "#c6dbc9"),
        Rejected => ("#f1e2da", "#94473e", "#e4c8bf"),
        _ => ("#efefe7", "#45594f", "#e1e5da")
    };
}
