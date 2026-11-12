using BATT_PPMS.Data;
using BATT_PPMS.Models;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Helpers;

/// <summary>
/// Đồng bộ trạng thái máy với yêu cầu sửa chữa:
/// - Yêu cầu có đánh dấu "Máy đã dừng hoạt động" thì máy chuyển sang Down và bị khóa (không ai đổi tay được)
///   cho tới khi yêu cầu kết thúc (hoàn thành / hủy / bị từ chối).
/// - Không có yêu cầu đang mở thì Manager / Owner đổi trạng thái bình thường.
/// </summary>
public static class MachineStatusGuard
{
    public const string Active = "Active";
    public const string Warning = "Warning";
    public const string Down = "Down";

    // Các trạng thái yêu cầu còn "mở": máy vẫn bị coi là đang hỏng
    private static readonly string[] OpenStatuses =
    {
        RepairStatus.Pending, "Reported",
        RepairStatus.Assigned,
        RepairStatus.InProgress, "In Progress",
        RepairStatus.WaitingMaterial
    };

    private static IQueryable<MaintenanceRequest> OpenStoppedRequests(AppDbContext db) =>
        db.MaintenanceRequests.Where(r => r.MachineStopped
                                          && r.Status != null
                                          && OpenStatuses.Contains(r.Status));

    /// <summary>Máy đang bị khóa trạng thái vì có báo cáo ngừng hoạt động chưa sửa xong.</summary>
    public static Task<bool> IsLockedAsync(AppDbContext db, int machineId) =>
        OpenStoppedRequests(db).AnyAsync(r => r.BrokenReport.MachineId == machineId);

    /// <summary>Tập id máy đang bị khóa (dùng cho danh sách).</summary>
    public static async Task<HashSet<int>> LockedMachineIdsAsync(AppDbContext db) =>
        (await OpenStoppedRequests(db).Select(r => r.BrokenReport.MachineId).Distinct().ToListAsync()).ToHashSet();

    /// <summary>Đặt máy sang Ngừng hoạt động (gọi khi báo cáo có tick "máy đã dừng").</summary>
    public static void MarkDown(Machine machine) => machine.Status = Down;

    /// <summary>
    /// Mở khóa máy khi một yêu cầu kết thúc: nếu không còn yêu cầu dừng máy nào khác đang mở
    /// và máy đang Down thì trả về Hoạt động. Gọi TRƯỚC SaveChanges.
    /// </summary>
    public static async Task ReleaseAsync(AppDbContext db, int machineId, int excludeRequestId)
    {
        var stillLocked = await OpenStoppedRequests(db)
            .AnyAsync(r => r.BrokenReport.MachineId == machineId && r.RequestId != excludeRequestId);
        if (stillLocked) return;

        var machine = await db.Machines.FindAsync(machineId);
        if (machine != null && machine.Status == Down)
            machine.Status = Active;
    }
}
