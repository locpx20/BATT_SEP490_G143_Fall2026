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
    private async Task NotifyManagers(string message) {
        var ids=await db.Users.Where(x=>x.Status=="Active"&&x.Role.RoleName==RoleConstants.Manager).Select(x=>x.UserId).ToListAsync();
        foreach(var id in ids) Notifier.Add(db,id,"Thông tin nhập kho thành phẩm",message,"/Warehouse/Index?tab=receipts");
    }
    private async Task<T> Change<T>(Func<Task<T>> action) {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var result = await action(); await db.SaveChangesAsync(); await tx.CommitAsync(); return result;
    }
    private async Task<PersonnelAssignment> OwnAssignment(int id, int staff) {
        var a = await db.PersonnelAssignments.SingleOrDefaultAsync(x => x.AssignmentId == id && x.StaffId == staff);
        Need(a != null && a.Status != "Cancelled", "Không tìm thấy phân công hợp lệ của bạn.");
        return a!;
    }
    public async Task<List<PersonnelAssignment>> AssignedShiftOptions(int staff, int preferredAssignmentId = 0)
    {
        var today = DateOnly.FromDateTime(Now);
        var assignments = await db.PersonnelAssignments.AsNoTracking()
            .Where(a => a.StaffId == staff && a.Status != "Cancelled" && a.AssignmentDate >= today)
            .OrderBy(a => a.AssignmentDate).ThenBy(a => a.Shift).ThenBy(a => a.AssignmentId)
            .ToListAsync();
        return assignments.GroupBy(a => new { a.AssignmentDate, a.Shift })
            .Select(shift => shift.FirstOrDefault(a => a.AssignmentId == preferredAssignmentId) ?? shift.First())
            .ToList();
    }
    public async Task<List<PersonnelAssignment>> ConsumptionShiftOptions(int staff, int editReportId = 0)
    {
        var today = DateOnly.FromDateTime(Now);
        var assignments = db.PersonnelAssignments.AsNoTracking()
            .Where(a => a.StaffId == staff && a.Status != "Cancelled");
        if (editReportId > 0)
            return await assignments.Where(a => db.MaterialUsageReviews.Any(review =>
                review.ReportId == editReportId && review.Report.StaffId == staff
                && review.Report.AssignmentId == a.AssignmentId)).ToListAsync();

        var available = await assignments
            .Where(a => a.AssignmentDate >= today
                && !db.MaterialUsageReviews.Any(review => review.Report.ShiftDate == a.AssignmentDate
                    && review.Report.Assignment.Shift == a.Shift))
            .OrderBy(a => a.AssignmentDate).ThenBy(a => a.Shift).ThenBy(a => a.AssignmentId)
            .ToListAsync();
        return available.GroupBy(a => new { a.AssignmentDate, a.Shift }).Select(shift => shift.First()).ToList();
    }

    public Task<int> RequestQc(int staff, int assignmentId, int quantity, string? note) => Change(async () => {
        await Actor(staff, RoleConstants.Staff); var a = await OwnAssignment(assignmentId, staff);
        Need(a.AssignmentDate >= DateOnly.FromDateTime(Now), "Chỉ được tạo yêu cầu QC từ ngày hôm nay trở đi.");
        Need(quantity > 0, "Sản lượng cần kiểm tra phải lớn hơn 0 kg.");
        var text = Text(note, 5000, "ghi chú");
        Need(!await db.ProductionQualityRequests.AnyAsync(x =>
            x.Assignment.AssignmentDate == a.AssignmentDate && x.Assignment.Shift == a.Shift
            && (x.Inspection.Status != "Completed" || x.Inspection.Result != "Fail")),
            "Ca này đã có lô thành phẩm gửi kiểm tra. Mỗi ca chỉ gửi một lô tổng.");
        var batch = new FinishedGoodBatch { BagNumber = LotCode.Create(a.AssignmentDate), ProductionDate = a.AssignmentDate, Quantity = quantity, CreatedBy = staff, Status = "Pending QC" };
        var inspection = new Qcinspection { InspectionCode = $"QC-{Guid.NewGuid():N}", Batch = batch, RequestedBy = staff, RequestDate = Now, Status = "Pending", Note = text };
        var request = new ProductionQualityRequest { Assignment = a, Inspection = inspection, CreatedAt = Now };
        db.ProductionQualityRequests.Add(request); await db.SaveChangesAsync();
        var inspectors=await db.Users.Where(x=>x.Status=="Active"&&x.Role.RoleName==RoleConstants.QCStaff).Select(x=>x.UserId).ToListAsync();
        foreach(var id in inspectors) Notifier.Add(db,id,"Yêu cầu kiểm tra chất lượng mới",$"{inspection.InspectionCode}: kiểm tra lô {batch.BagNumber}, {quantity:N0} kg.","/QC/Index");
        return request.RequestId;
    });
    public Task<int> StartQc(int qc, int requestId) => Change(async () => {
        await Actor(qc, RoleConstants.QCStaff);
        var request = await QualityQuery.SingleOrDefaultAsync(x => x.RequestId == requestId);
        Need(request?.Inspection.Status == "Pending", "Yêu cầu không còn ở trạng thái chờ tiếp nhận.");
        request!.Inspection.Status = "In Progress"; request.Inspection.InspectedBy = qc; return requestId;
    });
    public Task<int> FinishQc(int qc, int requestId, string? result, string? note, byte[]? image = null) => Change(async () => {
        await Actor(qc, RoleConstants.QCStaff);
        Need(result is "Pass" or "Fail", "Vui lòng chọn kết quả đạt hoặc không đạt.");
        var text = Text(note, 5000, "kết luận kiểm tra", true);
        var request = await QualityQuery.SingleOrDefaultAsync(x => x.RequestId == requestId);
        Need(request?.Inspection.Status == "In Progress" && request.Inspection.InspectedBy == qc, "Chỉ nhân viên QC đã tiếp nhận được trả kết quả một lần.");
        var inspection = request!.Inspection;
        request.ResultImageType = QcEvidence.Validate(image); request.ResultImage = image;
        inspection.Result = result; inspection.Note = string.Join("\n", new[] { inspection.Note, $"Kết luận QC: {text}" }.Where(x => !string.IsNullOrWhiteSpace(x)));
        inspection.InspectDate = Now; inspection.Status = "Completed";
        inspection.Batch.Status = result == "Pass" ? "Pending Receipt" : "QC Failed";
        if(inspection.RequestedBy.HasValue) Notifier.Add(db,inspection.RequestedBy.Value,"QC đã trả kết quả kiểm tra",$"{inspection.InspectionCode}: {Label(result)}. {text}",result=="Pass"?$"/Warehouse/Index?tab=receipts&QualityRequestId={requestId}":"/QC/Index");
        return requestId;
    });
    private async Task SetUsage(EndOfShiftReport report, List<MaterialUsageInput>? input) {
        var rows = (input ?? new()).Where(x => x.Quantity.HasValue && x.Quantity != 0).ToList();
        Need(rows.Count > 0, "Vui lòng nhập nguyên liệu thực tế đã sử dụng trong ca.");
        Need(rows.All(x => x.Quantity > 0 && x.Quantity <= 9999999999.99m && decimal.Round(x.Quantity!.Value, 2) == x.Quantity), "Lượng nguyên liệu phải lớn hơn 0, tối đa 2 chữ số thập phân.");
        Need(rows.Select(x => x.MaterialId).Distinct().Count() == rows.Count, "Không được khai báo trùng nguyên liệu.");
        var ids = rows.Select(x => x.MaterialId).ToList();
        Need(await db.RawMaterials.CountAsync(x => ids.Contains(x.MaterialId)) == rows.Count, "Nguyên liệu không tồn tại.");
        var previous = report.ReportId == 0 ? new List<ShiftMaterialUsage>() : await db.ShiftMaterialUsages.Where(x => x.ReportId == report.ReportId).ToListAsync();
        if (previous.Any(x => x.ExportId != null)) {
            Need(previous.Count == rows.Count && previous.All(x => rows.Any(r => r.MaterialId == x.MaterialId && r.Quantity == x.Quantity)), "Thủ kho đã duyệt và trừ tồn. Chỉ được sửa ghi chú, không được thay đổi tiêu hao.");
            return;
        }
        db.ShiftMaterialUsages.RemoveRange(previous.Where(x => !ids.Contains(x.MaterialId)));
        foreach(var row in rows) {
            var usage = previous.SingleOrDefault(x => x.MaterialId == row.MaterialId);
            if (usage == null) { usage = new() { Report = report, MaterialId = row.MaterialId }; db.ShiftMaterialUsages.Add(usage); }
            usage.Quantity = row.Quantity!.Value;
        }
    }
    public async Task<int> SaveConsumptionKg(int staff, int assignmentId, decimal quantityKg, string? note, int reportId = 0) {
        Need(quantityKg > 0 && quantityKg <= 9999999999.99m && decimal.Round(quantityKg,2) == quantityKg, "Khối lượng phải lớn hơn 0 kg và tối đa 2 chữ số thập phân.");
        var materials=await db.RawMaterials.AsNoTracking().Where(x=>x.Unit.Trim().ToLower()=="kg").OrderBy(x=>x.MaterialId).ToListAsync();
        Need(materials.Count>0, "Kho chưa có nguyên liệu theo đơn vị kg.");
        Need(materials.Select(x=>x.MaterialName.Trim().ToUpperInvariant()).Distinct().Count()==1, "Kho có nhiều loại nguyên liệu khác nhau. Cần xác định loại nguyên liệu sản xuất trước khi khai báo.");
        return await SaveConsumption(staff,assignmentId,new(){new(){MaterialId=materials[0].MaterialId,Quantity=quantityKg}},note,reportId);
    }
    public Task<int> SaveConsumption(int staff, int assignmentId, List<MaterialUsageInput> materials, string? note, int reportId = 0) => Change(async () => {
        await Actor(staff, RoleConstants.Staff); var assignment = await OwnAssignment(assignmentId, staff);
        Need(reportId > 0 || assignment.AssignmentDate >= DateOnly.FromDateTime(Now), "Chỉ được tạo phiếu tiêu hao từ ngày hôm nay trở đi.");
        Need(!await db.MaterialUsageReviews.AnyAsync(x => x.ReportId != reportId
            && x.Report.ShiftDate == assignment.AssignmentDate && x.Report.Assignment.Shift == assignment.Shift),
            "Ca này đã có phiếu tiêu hao. Người gửi có thể sửa và gửi lại phiếu hiện có nếu cần.");
        MaterialUsageReview review;
        EndOfShiftReport report;
        if(reportId > 0) {
            review = (await db.MaterialUsageReviews.Include(x => x.Report).SingleOrDefaultAsync(x => x.ReportId == reportId && x.Report.StaffId == staff))!;
            Need(review?.Status is "Pending" or "Rejected", "Chỉ được sửa khai báo của bạn chưa được Thủ kho duyệt.");
            report = review!.Report;
            Need(report.AssignmentId == assignmentId, "Vui lòng giữ nguyên ngày và ca làm việc của phiếu.");
        } else {
            report = new() { StaffId = staff, CreatedAt = Now, ProductionQuantity = 0 };
            review = new() { Report = report }; db.MaterialUsageReviews.Add(review);
        }
        report.Assignment = assignment; report.ShiftDate = assignment.AssignmentDate; report.Note = Text(note, 5000, "ghi chú tiêu hao"); report.Status = "Submitted";
        await SetUsage(report, materials); review.Status = "Pending"; review.ReviewedAt = null; review.ReviewedBy = null;
        await db.SaveChangesAsync();
        var keepers=await db.Users.Where(x=>x.Status=="Active"&&x.Role.RoleName==RoleConstants.WarehouseStaff).Select(x=>x.UserId).ToListAsync();
        foreach(var keeper in keepers)Notifier.Add(db,keeper,reportId>0?"Phiếu tiêu hao đã gửi lại":"Phiếu tiêu hao chờ duyệt",$"TH-{report.ReportId:D4}, ngày {report.ShiftDate:dd/MM/yyyy}, {assignment.Shift}. Staff đã gửi khối lượng thực dùng.","/Warehouse/Consumption?StatusFilter=Pending");
        return report.ReportId;
    });
    public Task<int> ReviewMaterialUsage(int keeper, int reportId, bool approve, string? reason) => Change(async () => {
        await Actor(keeper, RoleConstants.WarehouseStaff);
        var review = await db.MaterialUsageReviews.Include(x => x.Report).ThenInclude(x => x.Assignment).SingleOrDefaultAsync(x => x.ReportId == reportId);
        Need(review?.Status == "Pending", "Khai báo không còn ở trạng thái chờ Thủ kho duyệt.");
        var report = review!.Report;
        var usage = await db.ShiftMaterialUsages.Include(x => x.Material).Where(x => x.ReportId == reportId).OrderBy(x => x.MaterialId).ToListAsync();
        Need(usage.Count > 0, "Báo cáo chưa khai báo nguyên liệu thực dùng. Vui lòng trả lại để nhân viên bổ sung.");
        Need(usage.All(x => x.ExportId == null), "Báo cáo đã được trừ tồn kho.");
        if (!approve) {
            review.RejectionReason = Text(reason, 1000, "lý do từ chối", true);
            review.Status = "Rejected"; review.ReviewedBy = keeper; review.ReviewedAt = Now;
            if(report.StaffId.HasValue) Notifier.Add(db,report.StaffId.Value,"Khai báo tiêu hao cần chỉnh sửa",$"TH-{reportId:D4}: {review.RejectionReason}", $"/Warehouse/Consumption?EditId={reportId}");
            return reportId;
        }
        Need(!await db.MaterialUsageReviews.AnyAsync(x => x.ReportId != reportId && x.Status == "Approved"
            && x.Report.ShiftDate == report.ShiftDate && x.Report.Assignment.Shift == report.Assignment.Shift),
            "Ca này đã có phiếu tiêu hao được duyệt. Không thể trừ tồn lần nữa cho cùng ca.");
        var hasShiftPlan = await db.ShiftProductionPlans.AnyAsync(plan => plan.PlanDate == report.ShiftDate
            && plan.Shift == report.Assignment.Shift && plan.ExpectedMaterialKg > 0);
        Need(hasShiftPlan || await db.DailyMaterialPlans.AnyAsync(plan => plan.PlanDate == report.ShiftDate && plan.ExpectedKg > 0),
            $"Manager chưa nhập nguyên liệu dự kiến cho {report.Assignment.Shift ?? "ca này"} ngày {report.ShiftDate:dd/MM/yyyy}.");
        var singleKindKg=usage.All(x=>x.Material.Unit.Trim().Equals("kg",StringComparison.OrdinalIgnoreCase))
            && usage.Select(x=>x.Material.MaterialName.Trim().ToUpperInvariant()).Distinct().Count()==1;
        if(singleKindKg) {
            var name=usage[0].Material.MaterialName.Trim();
            var kgStock=await db.RawMaterials.Where(x=>x.Unit.Trim().ToLower()=="kg").OrderBy(x=>x.MaterialId).ToListAsync();
            var stock=kgStock.Where(x=>x.MaterialName.Trim().Equals(name,StringComparison.OrdinalIgnoreCase)).ToList();
            var remaining=usage.Sum(x=>x.Quantity);
            Need(stock.Sum(x=>x.CurrentQuantity)>=remaining, "Tổng tồn nguyên liệu không đủ để ghi nhận tiêu hao.");
            var usedIds=new HashSet<int>();
            foreach(var material in stock) {
                if(remaining==0) break;
                var amount=Math.Min(remaining,material.CurrentQuantity);
                if(amount<=0) continue;
                var row=usage.SingleOrDefault(x=>x.MaterialId==material.MaterialId);
                if(row==null){row=new(){Report=report,MaterialId=material.MaterialId};db.ShiftMaterialUsages.Add(row);}
                row.Quantity=amount; material.CurrentQuantity-=amount; remaining-=amount; usedIds.Add(material.MaterialId);
                row.Export=new RawMaterialExport{MaterialId=material.MaterialId,Quantity=amount,ExportedBy=keeper,ExportDate=Now,Note=$"Tiêu hao TH-{reportId:D4}, ngày {report.ShiftDate:dd/MM/yyyy}, {report.Assignment.Shift}"};
            }
            db.ShiftMaterialUsages.RemoveRange(usage.Where(x=>!usedIds.Contains(x.MaterialId)));
        } else {
            foreach(var row in usage) {
                Need(row.Material.CurrentQuantity >= row.Quantity, $"Tồn kho {row.Material.MaterialName} không đủ để ghi nhận tiêu hao.");
                row.Material.CurrentQuantity -= row.Quantity;
                row.Export = new RawMaterialExport { MaterialId = row.MaterialId, Quantity = row.Quantity, ExportedBy = keeper, ExportDate = Now, Note = $"Thủ kho duyệt tiêu hao RP-{reportId:D4}, ngày {report.ShiftDate:dd/MM/yyyy}, {report.Assignment.Shift}" };
            }
        }
        review.Status = "Approved"; review.ReviewedBy = keeper; review.ReviewedAt = Now;
        report.Status = "Approved";
        return reportId;
    });
    public Task<int> RequestReceipt(int staff, int requestId, string? note = null) => Change(async () => {
        await Actor(staff, RoleConstants.Staff);
        var q = await QualityQuery.SingleOrDefaultAsync(x => x.RequestId == requestId);
        Need(q?.Inspection.RequestedBy == staff && q.Assignment.StaffId == staff, "Không tìm thấy yêu cầu QC của bạn.");
        Need(q!.Inspection.Status == "Completed" && q.Inspection.Result == "Pass", "Chỉ được yêu cầu nhập kho sau khi QC trả kết quả đạt.");
        Need(q.Assignment.Status != "Cancelled", "Phân công đã bị hủy.");
        Need(!await db.FinishedGoodsReceipts.AnyAsync(x => x.QualityRequestId != requestId
            && x.QualityRequest.Assignment.AssignmentDate == q.Assignment.AssignmentDate
            && x.QualityRequest.Assignment.Shift == q.Assignment.Shift),
            "Ca này đã có yêu cầu nhập lô thành phẩm tổng.");
        if (q.Receipt != null) {
            Need(q.Receipt.Status == "Rejected", "Yêu cầu nhập kho đã được gửi hoặc xử lý.");
            q.Receipt.RequestNote = Text(note, 2000, "nội dung điều chỉnh", true);
            q.Receipt.Status = "Pending"; q.Receipt.RequestedAt = Now;
        } else db.FinishedGoodsReceipts.Add(new() { QualityRequest = q, RequestedBy = staff, RequestedAt = Now, ExpectedQuantity = q.Inspection.Batch.Quantity, RequestNote = Text(note, 2000, "ghi chú yêu cầu") });
        var keepers=await db.Users.Where(x=>x.Status=="Active"&&x.Role.RoleName==RoleConstants.WarehouseStaff).Select(x=>x.UserId).ToListAsync();
        foreach(var keeper in keepers)Notifier.Add(db,keeper,"Yêu cầu nhập thành phẩm chờ duyệt",$"Lô {q.Inspection.Batch.BagNumber}, {q.Inspection.Batch.Quantity:N2} kg. QC đã trả kết quả đạt.","/Warehouse/Index?tab=receipts&StatusFilter=Pending");
        return requestId;
    });
    public Task<int> RejectReceipt(int keeper, int receiptId, string? reason) => Change(async () => {
        await Actor(keeper, RoleConstants.WarehouseStaff); var text = Text(reason, 1000, "lý do từ chối", true);
        var r = await db.FinishedGoodsReceipts.SingleOrDefaultAsync(x => x.ReceiptId == receiptId);
        Need(r?.Status == "Pending", "Yêu cầu đã được xử lý."); r!.Status = "Rejected"; r.RejectionReason = text; r.ReceivedBy = keeper;
        if(r.RequestedBy.HasValue) Notifier.Add(db,r.RequestedBy.Value,"Yêu cầu nhập kho bị từ chối",$"NK-{receiptId:D4}: {text}","/Warehouse/Index?tab=receipts");
        return receiptId;
    });
    public Task<int> ReceiveGoods(int keeper, int receiptId, decimal actual, string? note) => Change(async () => {
        await Actor(keeper, RoleConstants.WarehouseStaff);
        Need(actual > 0 && actual <= 9999999999.99m && decimal.Round(actual, 2) == actual, "Khối lượng thực nhập phải lớn hơn 0, tối đa 2 chữ số thập phân.");
        var r = await db.FinishedGoodsReceipts.Include(x => x.QualityRequest).ThenInclude(x => x.Inspection).ThenInclude(x => x.Batch)
            .Include(x => x.QualityRequest).ThenInclude(x => x.Report)
            .Include(x => x.QualityRequest).ThenInclude(x => x.Assignment)
            .SingleOrDefaultAsync(x => x.ReceiptId == receiptId);
        Need(r?.Status == "Pending", "Yêu cầu đã được xử lý; không thể nhập kho lần nữa.");
        Need(r!.QualityRequest.Inspection.Status == "Completed" && r.QualityRequest.Inspection.Result == "Pass", "Lô hàng chưa có kết quả QC đạt.");
        var assignment = r.QualityRequest.Assignment;
        Need(!await db.FinishedGoodsReceipts.AnyAsync(x => x.ReceiptId != receiptId && x.ReceivedAt != null && x.ActualQuantity > 0
            && x.QualityRequest.Assignment.AssignmentDate == assignment.AssignmentDate
            && x.QualityRequest.Assignment.Shift == assignment.Shift),
            "Ca này đã nhập lô thành phẩm tổng vào kho.");
        var text = Text(note, 2000, "ghi chú đối chiếu", actual != r.ExpectedQuantity);
        r.ActualQuantity = actual; r.DiscrepancyNote = text; r.ReceivedBy = keeper; r.ReceivedAt = Now;
        // Preserve the user's diagram: discrepancy Yes -> Manager; No -> Staff confirmation -> Manager.
        r.Status = actual != r.ExpectedQuantity ? "Received" : "AwaitingStaffConfirmation";
        r.QualityRequest.Inspection.Batch.Quantity = actual;
        r.QualityRequest.Inspection.Batch.Status = "In Stock";
        var keepers = await db.Users.Where(u => u.Status == "Active" && u.Role.RoleName == RoleConstants.WarehouseStaff)
            .Select(u => u.UserId).ToListAsync();
        foreach (var id in keepers) Notifier.Add(db,id,"Kho thành phẩm đã cập nhật",
            $"Lô {r.QualityRequest.Inspection.Batch.BagNumber} đã nhập {actual:N2} kg theo phiếu NK-{receiptId:D4}.",
            "/Inventory/Index?tab=finished");
        if(actual!=r.ExpectedQuantity) await NotifyManagers($"Phiếu NK-{receiptId:D4} đã nhập kho, chênh lệch {actual-r.ExpectedQuantity:N2} kg. {text}");
        else if(r.RequestedBy.HasValue) Notifier.Add(db,r.RequestedBy.Value,"Xác nhận phiếu nhập thành phẩm",$"Phiếu NK-{receiptId:D4} chờ bạn xác nhận.","/Warehouse/Index?tab=receipts");
        return receiptId;
    });
    public Task<int> ConfirmReceipt(int staff, int receiptId) => Change(async () => {
        await Actor(staff, RoleConstants.Staff);
        var r = await db.FinishedGoodsReceipts.SingleOrDefaultAsync(x => x.ReceiptId == receiptId && x.RequestedBy == staff);
        Need(r?.Status == "AwaitingStaffConfirmation", "Phiếu không còn chờ xác nhận của bạn.");
        r!.StaffConfirmedAt = Now; r.Status = "Received";
        await NotifyManagers($"Staff đã xác nhận phiếu NK-{receiptId:D4}. Nhập kho thành phẩm hoàn tất.");
        return receiptId;
    });
}
