namespace BATT_PPMS.Helpers
{
    /// <summary>
    /// Định nghĩa đầy đủ 7 vai trò trong hệ thống (khớp toàn bộ use case diagram)
    /// </summary>
    public static class RoleConstants
    {
        // UC Admin: Quản lý người dùng hệ thống
        public const string Admin = "Admin";

        // UC Owner: Xem dashboard, báo cáo, quản lý Supplier/Customer
        public const string Owner = "Owner";

        // UC Manager: Quản lý đơn hàng, lịch làm việc, duyệt bảo trì/mua hàng, tạo phiếu QC
        public const string Manager = "Manager";

        // UC Staff: Xem lịch, nộp báo cáo ca làm, báo cáo thiết bị hỏng
        public const string Staff = "Staff";

        // UC Maintenance Staff: Yêu cầu sửa chữa, mua vật tư, nộp báo cáo vật tư
        public const string MaintenanceStaff = "Maintenance Staff";

        // UC QC Staff: Xem phiếu kiểm tra QC, nộp/sửa kết quả kiểm tra
        public const string QCStaff = "QC Staff";

        // UC Purchasing Staff: Quản lý đơn mua hàng (tạo/sửa/xoá), xem nhà cung cấp
        public const string PurchasingStaff = "Purchasing Staff";
    }

    /// <summary>
    /// Định nghĩa các trạng thái dùng chung trong hệ thống
    /// </summary>
    public static class StatusConstants
    {
        // Chung
        public const string Active = "Active";
        public const string Inactive = "Inactive";

        // Đơn hàng / Order
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";

        // QC Inspection
        public const string Pass = "Pass";
        public const string Fail = "Fail";

        // Giao hàng / Delivery
        public const string Prepared = "Prepared";
        public const string InTransit = "In Transit";
        public const string Delivered = "Delivered";

        // Thiết bị hỏng / Equipment
        public const string Reported = "Reported";
        public const string UnderMaintenance = "Under Maintenance";
        public const string Fixed = "Fixed";

        // Phân công / Assignment
        public const string Assigned = "Assigned";
        public const string InProgress = "In Progress";
        public const string Done = "Done";

        // Mức độ nghiêm trọng
        public const string Low = "Low";
        public const string Medium = "Medium";
        public const string High = "High";
        public const string Critical = "Critical";
    }

    /// <summary>
    /// Các key Session dùng trong hệ thống
    /// </summary>
    public static class SessionKeys
    {
        public const string UserId = "UserId";
        public const string Username = "Username";
        public const string FullName = "FullName";
        public const string RoleId = "RoleId";
        public const string RoleName = "RoleName";
    }
}
