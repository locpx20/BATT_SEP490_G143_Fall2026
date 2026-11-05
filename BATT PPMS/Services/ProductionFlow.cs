using System.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Services;

public sealed class ProductionFlowException(string message) : Exception(message);

/// <summary>Enforces production handoffs and posts stock exactly once, in one transaction.</summary>
public sealed class ProductionFlow(ProductionManagementDbContext db)
{
    public static DateTime Now => DateTime.UtcNow.AddHours(7);
    public static string Label(string? status) => status switch {
        "Pending" => "Chờ tiếp nhận", "In Progress" => "Đang kiểm tra", "Completed" => "Đã trả kết quả",
        "Pass" => "Đạt", "Fail" => "Không đạt", "Rejected" => "Bị từ chối", "Issued" => "Đã cấp nguyên liệu",
        "Received" => "Đã nhập kho", "AwaitingStaffConfirmation" => "Chờ Staff xác nhận",
        "Submitted" => "Chờ Manager duyệt", "Approved" => "Đã duyệt", _ => status ?? "Chưa có" };

    public IQueryable<ProductionQualityRequest> QualityQuery => db.ProductionQualityRequests
        .Include(x => x.Assignment).ThenInclude(x => x.AssignedByNavigation)
        .Include(x => x.Inspection).ThenInclude(x => x.Batch)
        .Include(x => x.Inspection).ThenInclude(x => x.RequestedByNavigation)
        .Include(x => x.Inspection).ThenInclude(x => x.InspectedByNavigation)
        .Include(x => x.Report).Include(x => x.Receipt);

    private static void Need(bool condition, string message) { if (!condition) throw new ProductionFlowException(message); }
    private static string? Text(string? text, int max, string label, bool required = false) {
        var value = InputValidator.Clean(text);
        Need(!required || !string.IsNullOrWhiteSpace(value), $"Vui lòng nhập {label}.");
        Need(value == null || value.Length <= max, $"{label} tối đa {max} ký tự.");
        return value;
    }
    private async Task Actor(int userId, string role) => Need(await db.Users.AnyAsync(x => x.UserId == userId && x.Status == "Active" && x.Role.RoleName == role), "Tài khoản không có quyền thực hiện thao tác này.");
    private async Task<T> Change<T>(Func<Task<T>> action) {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var result = await action(); await db.SaveChangesAsync(); await tx.CommitAsync(); return result;
    }
    private async Task<PersonnelAssignment> OwnAssignment(int id, int staff) {
        var a = await db.PersonnelAssignments.SingleOrDefaultAsync(x => x.AssignmentId == id && x.StaffId == staff);
        Need(a != null && a.Status != "Cancelled", "Không tìm thấy phân công hợp lệ của bạn.");
        Need(a!.AssignmentDate <= DateOnly.FromDateTime(Now), "Chưa đến ngày sản xuất của phân công này.");
        return a;
    }
    public Task<int> RequestQc(int staff, int assignmentId, int quantity, string? note) => Change(async () => {
        await Actor(staff, RoleConstants.Staff); var a = await OwnAssignment(assignmentId, staff);
        Need(quantity > 0, "Sản lượng cần kiểm tra phải lớn hơn 0 kg.");
        var text = Text(note, 5000, "ghi chú");
        Need(!await db.ProductionQualityRequests.AnyAsync(x => x.Inspection.RequestedBy == staff
            && x.Assignment.AssignmentDate == a.AssignmentDate && x.Assignment.Shift == a.Shift
            && x.Inspection.Status != "Completed"), "Bạn đã có yêu cầu QC đang chờ cho ca này. Vui lòng chờ QC trả kết quả.");
        var batch = new FinishedGoodBatch { BagNumber = $"LOT-{a.AssignmentDate:yyyyMMdd}-{Guid.NewGuid():N}"[..38], ProductionDate = a.AssignmentDate, Quantity = quantity, CreatedBy = staff, Status = "Pending QC" };
        var inspection = new Qcinspection { InspectionCode = $"QC-{Guid.NewGuid():N}", Batch = batch, RequestedBy = staff, RequestDate = Now, Status = "Pending", Note = text };
        var request = new ProductionQualityRequest { Assignment = a, Inspection = inspection, CreatedAt = Now };
        db.ProductionQualityRequests.Add(request); await db.SaveChangesAsync(); return request.RequestId;
    });
    public Task<int> StartQc(int qc, int requestId) => Change(async () => {
        await Actor(qc, RoleConstants.QCStaff);
        var request = await QualityQuery.SingleOrDefaultAsync(x => x.RequestId == requestId);
        Need(request?.Inspection.Status == "Pending", "Yêu cầu không còn ở trạng thái chờ tiếp nhận.");
        request!.Inspection.Status = "In Progress"; request.Inspection.InspectedBy = qc; return requestId;
    });
    public Task<int> FinishQc(int qc, int requestId, string? result, string? note) => Change(async () => {
        await Actor(qc, RoleConstants.QCStaff);
        Need(result is "Pass" or "Fail", "Vui lòng chọn kết quả đạt hoặc không đạt.");
        var text = Text(note, 5000, "kết luận kiểm tra", true);
        var request = await QualityQuery.SingleOrDefaultAsync(x => x.RequestId == requestId);
        Need(request?.Inspection.Status == "In Progress" && request.Inspection.InspectedBy == qc, "Chỉ nhân viên QC đã tiếp nhận được trả kết quả một lần.");
        var inspection = request!.Inspection;
        inspection.Result = result; inspection.Note = string.Join("\n", new[] { inspection.Note, $"Kết luận QC: {text}" }.Where(x => !string.IsNullOrWhiteSpace(x)));
        inspection.InspectDate = Now; inspection.Status = "Completed";
        inspection.Batch.Status = result == "Pass" ? "Pending Receipt" : "QC Failed";
        return requestId;
    });
    private async Task<ProductionQualityRequest> ReturnedQc(int staff, int requestId) {
        var q = await QualityQuery.SingleOrDefaultAsync(x => x.RequestId == requestId);
        Need(q?.Inspection.RequestedBy == staff && q.Assignment.StaffId == staff, "Không tìm thấy kết quả QC của bạn.");
        Need(q!.Inspection.Status == "Completed" && q.Inspection.Result is "Pass" or "Fail", "QC chưa trả kết quả. Bạn chưa thể lập báo cáo cuối ca.");
        return q;
    }
    private async Task SetUsage(EndOfShiftReport report, List<MaterialUsageInput>? input) {
        var rows = (input ?? new()).Where(x => x.Quantity.HasValue && x.Quantity != 0).ToList();
        Need(rows.Count > 0, "Vui lòng nhập nguyên liệu thực tế đã sử dụng trong ca.");
        Need(rows.All(x => x.Quantity > 0 && x.Quantity <= 9999999999.99m && decimal.Round(x.Quantity!.Value, 2) == x.Quantity), "Lượng nguyên liệu phải lớn hơn 0, tối đa 2 chữ số thập phân.");
        Need(rows.Select(x => x.MaterialId).Distinct().Count() == rows.Count, "Không được khai báo trùng nguyên liệu.");
        var ids = rows.Select(x => x.MaterialId).ToList();
        Need(await db.RawMaterials.CountAsync(x => ids.Contains(x.MaterialId)) == rows.Count, "Nguyên liệu không tồn tại.");
        var previous = report.ReportId == 0 ? new List<ShiftMaterialUsage>() : await db.ShiftMaterialUsages.Where(x => x.ReportId == report.ReportId).ToListAsync();
        Need(previous.All(x => x.ExportId == null), "Báo cáo đã ghi nhận trừ tồn, không thể thay đổi tiêu hao.");
        db.ShiftMaterialUsages.RemoveRange(previous.Where(x => !ids.Contains(x.MaterialId)));
        foreach(var row in rows) {
            var usage = previous.SingleOrDefault(x => x.MaterialId == row.MaterialId);
            if (usage == null) { usage = new() { Report = report, MaterialId = row.MaterialId }; db.ShiftMaterialUsages.Add(usage); }
            usage.Quantity = row.Quantity!.Value;
        }
    }
    public Task<int> SubmitReport(int staff, int requestId, string? note, List<MaterialUsageInput>? materials = null) => Change(async () => {
        await Actor(staff, RoleConstants.Staff); var q = await ReturnedQc(staff, requestId);
        Need(q.ReportId == null, "Kết quả QC này đã được sử dụng để lập báo cáo ca.");
        Need(q.Assignment.Status != "Cancelled", "Phân công sản xuất đã bị hủy.");
        q.Report = new EndOfShiftReport { AssignmentId = q.AssignmentId, StaffId = staff, ShiftDate = q.Assignment.AssignmentDate,
            ProductionQuantity = checked((int)q.Inspection.Batch.Quantity), Note = Text(note, 5000, "ghi chú báo cáo"), Status = "Submitted", CreatedAt = Now };
        await SetUsage(q.Report, materials);
        await db.SaveChangesAsync(); return q.Report.ReportId;
    });
    public Task<int> UpdateReport(int staff, int reportId, int requestId, string? note, List<MaterialUsageInput>? materials = null) => Change(async () => {
        await Actor(staff, RoleConstants.Staff);
        var report = await db.EndOfShiftReports.SingleOrDefaultAsync(x => x.ReportId == reportId && x.StaffId == staff);
        Need(report?.Status is "Submitted" or "Pending" or "Rejected", "Chỉ được sửa báo cáo của bạn đang chờ duyệt hoặc bị từ chối.");
        var existing = await db.ProductionQualityRequests.SingleOrDefaultAsync(x => x.ReportId == reportId);
        if (existing != null) Need(requestId == existing.RequestId, "Không thể thay lô QC của báo cáo đã lập. Chỉ chỉnh sửa ghi chú.");
        var q = await ReturnedQc(staff, requestId);
        Need(q.ReportId == null || q.ReportId == reportId, "Kết quả QC đã gắn với báo cáo khác.");
        report!.AssignmentId = q.AssignmentId; report.ShiftDate = q.Assignment.AssignmentDate;
        report.ProductionQuantity = checked((int)q.Inspection.Batch.Quantity); report.Note = Text(note, 5000, "ghi chú báo cáo"); report.Status = "Submitted";
        await SetUsage(report, materials);
        q.Report = report; return reportId;
    });
    public Task<int> ApproveReport(int manager, int reportId) => Change(async () => {
        await Actor(manager, RoleConstants.Manager);
        var q = await QualityQuery.SingleOrDefaultAsync(x => x.ReportId == reportId);
        Need(q != null, "Báo cáo chưa liên kết kết quả QC. Staff cần bổ sung kết quả QC trước khi duyệt.");
        Need(q!.Report?.Status is "Submitted" or "Pending", "Báo cáo không còn ở trạng thái chờ duyệt.");
        Need(q.Inspection.Status == "Completed" && q.Inspection.Result is "Pass" or "Fail", "QC chưa trả kết quả kiểm tra.");
        var usage = await db.ShiftMaterialUsages.Include(x => x.Material).Where(x => x.ReportId == reportId).OrderBy(x => x.MaterialId).ToListAsync();
        Need(usage.Count > 0, "Báo cáo chưa khai báo nguyên liệu thực dùng. Vui lòng trả lại để nhân viên bổ sung.");
        Need(usage.All(x => x.ExportId == null), "Báo cáo đã được trừ tồn kho.");
        foreach(var row in usage) {
            Need(row.Material.CurrentQuantity >= row.Quantity, $"Tồn kho {row.Material.MaterialName} không đủ để ghi nhận tiêu hao.");
            row.Material.CurrentQuantity -= row.Quantity;
            row.Export = new RawMaterialExport { MaterialId = row.MaterialId, Quantity = row.Quantity, ExportedBy = manager, ExportDate = Now, Note = $"Tiêu hao báo cáo RP-{reportId:D4}, ngày {q.Report!.ShiftDate:dd/MM/yyyy}, {q.Assignment.Shift}" };
        }
        q.Report!.Status = "Approved"; q.Report.Note = NoteTags.Remove(q.Report.Note, NoteTags.Rejected); return reportId;
    });
    public Task<int> RejectReport(int manager, int reportId, string? reason) => Change(async () => {
        await Actor(manager, RoleConstants.Manager); var text = Text(reason, 500, "lý do từ chối", true);
        var report = await db.EndOfShiftReports.SingleOrDefaultAsync(x => x.ReportId == reportId);
        Need(report?.Status is "Submitted" or "Pending", "Báo cáo không còn ở trạng thái chờ duyệt.");
        report!.Status = "Rejected"; report.Note = NoteTags.Set(report.Note, NoteTags.Rejected, text!); return reportId;
    });
    public Task<int> RequestReceipt(int staff, int requestId, string? note = null) => Change(async () => {
        await Actor(staff, RoleConstants.Staff);
        var q = await QualityQuery.SingleOrDefaultAsync(x => x.RequestId == requestId);
        Need(q?.Inspection.RequestedBy == staff && q.Report?.StaffId == staff, "Không tìm thấy báo cáo và yêu cầu QC của bạn.");
        Need(q!.Inspection.Status == "Completed" && q.Inspection.Result == "Pass" && q.Report!.Status == "Approved", "Chỉ được yêu cầu nhập kho sau khi QC đạt và Manager đã duyệt báo cáo ca.");
        if (q.Receipt != null) {
            Need(q.Receipt.Status == "Rejected", "Yêu cầu nhập kho đã được gửi hoặc xử lý.");
            q.Receipt.RequestNote = Text(note, 2000, "nội dung điều chỉnh", true);
            q.Receipt.Status = "Pending"; q.Receipt.RequestedAt = Now;
        } else db.FinishedGoodsReceipts.Add(new() { QualityRequest = q, RequestedBy = staff, RequestedAt = Now, ExpectedQuantity = q.Inspection.Batch.Quantity, RequestNote = Text(note, 2000, "ghi chú yêu cầu") });
        return requestId;
    });
    public Task<int> RejectReceipt(int keeper, int receiptId, string? reason) => Change(async () => {
        await Actor(keeper, RoleConstants.WarehouseStaff); var text = Text(reason, 1000, "lý do từ chối", true);
        var r = await db.FinishedGoodsReceipts.SingleOrDefaultAsync(x => x.ReceiptId == receiptId);
        Need(r?.Status == "Pending", "Yêu cầu đã được xử lý."); r!.Status = "Rejected"; r.RejectionReason = text; r.ReceivedBy = keeper; return receiptId;
    });
    public Task<int> ReceiveGoods(int keeper, int receiptId, decimal actual, string? note) => Change(async () => {
        await Actor(keeper, RoleConstants.WarehouseStaff);
        Need(actual > 0 && actual <= 9999999999.99m && decimal.Round(actual, 2) == actual, "Khối lượng thực nhập phải lớn hơn 0, tối đa 2 chữ số thập phân.");
        var r = await db.FinishedGoodsReceipts.Include(x => x.QualityRequest).ThenInclude(x => x.Inspection).ThenInclude(x => x.Batch)
            .Include(x => x.QualityRequest).ThenInclude(x => x.Report).SingleOrDefaultAsync(x => x.ReceiptId == receiptId);
        Need(r?.Status == "Pending", "Yêu cầu đã được xử lý; không thể nhập kho lần nữa.");
        Need(r!.QualityRequest.Inspection.Status == "Completed" && r.QualityRequest.Inspection.Result == "Pass" && r.QualityRequest.Report?.Status == "Approved", "Lô hàng chưa đủ điều kiện QC và báo cáo ca.");
        var text = Text(note, 2000, "ghi chú đối chiếu", actual != r.ExpectedQuantity);
        r.ActualQuantity = actual; r.DiscrepancyNote = text; r.ReceivedBy = keeper; r.ReceivedAt = Now;
        // Preserve the user's diagram: discrepancy Yes -> Manager; No -> Staff confirmation -> Manager.
        r.Status = actual != r.ExpectedQuantity ? "Received" : "AwaitingStaffConfirmation";
        r.QualityRequest.Inspection.Batch.Quantity = actual;
        r.QualityRequest.Inspection.Batch.Status = "In Stock";
        return receiptId;
    });
    public Task<int> ConfirmReceipt(int staff, int receiptId) => Change(async () => {
        await Actor(staff, RoleConstants.Staff);
        var r = await db.FinishedGoodsReceipts.SingleOrDefaultAsync(x => x.ReceiptId == receiptId && x.RequestedBy == staff);
        Need(r?.Status == "AwaitingStaffConfirmation", "Phiếu không còn chờ xác nhận của bạn.");
        r!.StaffConfirmedAt = Now; r.Status = "Received"; return receiptId;
    });
}
