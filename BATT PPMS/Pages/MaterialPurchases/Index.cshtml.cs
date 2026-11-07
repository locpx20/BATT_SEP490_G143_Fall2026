using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.MaterialPurchases
{
    public class PurchaseRow
    {
        public int RequestId { get; set; }
        public string Code => $"MPR-{RequestId:D4}";
        public int MaintenanceRequestId { get; set; }
        public string MaintenanceCode => $"REQ-{MaintenanceRequestId:D4}";
        public string MachineName { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string? Unit { get; set; }
        public int Quantity { get; set; }
        public string RequestedByName { get; set; } = string.Empty;
        public DateTime RequestDate { get; set; }
        public string Status { get; set; } = PurchaseRequestStatus.Pending;
        public string? Note { get; set; }
        public string? ResultNote { get; set; }
        public decimal? TotalCost { get; set; }
        public string? HandledByName { get; set; }
        public DateTime? HandledAt { get; set; }
    }

    /// <summary>
    /// Mua vật tư sửa chữa: Maintenance Staff gửi yêu cầu khi thiếu vật tư (từ trang chi tiết yêu cầu sửa chữa),
    /// Manager đi mua rồi xác nhận "Đã mua" (hoặc từ chối). Khi không còn yêu cầu nào đang chờ, yêu cầu sửa chữa
    /// tự quay về "Đang sửa chữa" và nhân viên bảo trì được thông báo để tiếp tục.
    /// </summary>
    public class IndexModel : BasePageModel
    {
        private readonly AppDbContext _context;
        public IndexModel(AppDbContext context) { _context = context; }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public List<PurchaseRow> Rows { get; private set; } = new();
        public List<AppNotification> Notices { get; private set; } = new();
        public PageWindow Pagination { get; private set; } = new(0, 1);

        public int PendingCount { get; private set; }
        public int PurchasedCount { get; private set; }
        public int RejectedCount { get; private set; }

        // Chỉ Manager đi mua và xác nhận
        public bool CanHandle => IsManager;

        public string ScopeHint => IsMaintenanceStaff
            ? "Các yêu cầu mua vật tư do bạn gửi khi thiếu vật tư sửa chữa."
            : "Yêu cầu mua vật tư phục vụ sửa chữa. Mua xong hãy xác nhận để nhân viên bảo trì tiếp tục sửa.";

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        private static readonly string[] AllowedRoles =
            { RoleConstants.Owner, RoleConstants.Manager, RoleConstants.MaintenanceStaff };

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(AllowedRoles);
            if (check != null) return check;

            PageNumber = PageWindow.ReadRequestedPage(Request.Query, PageNumber);

            IQueryable<MaterialPurchaseRequest> scoped = _context.MaterialPurchaseRequests.AsNoTracking();
            if (IsMaintenanceStaff)
                scoped = scoped.Where(p => p.RequestedBy == CurrentUserId);

            var counts = await scoped
                .GroupBy(p => p.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();
            int Count(string status) => counts.Where(c => PurchaseRequestStatus.Normalize(c.Status) == status).Sum(c => c.Count);
            PendingCount = Count(PurchaseRequestStatus.Pending);
            PurchasedCount = Count(PurchaseRequestStatus.Purchased);
            RejectedCount = Count(PurchaseRequestStatus.Rejected);

            var query = scoped;

            if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "ALL")
            {
                var normalized = PurchaseRequestStatus.Normalize(StatusFilter);
                var statuses = PurchaseRequestStatus.Equivalents(normalized);
                query = normalized == PurchaseRequestStatus.Pending
                    ? query.Where(p => p.Status == null || statuses.Contains(p.Status))
                    : query.Where(p => p.Status != null && statuses.Contains(p.Status));
            }

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim();
                var idText = term.StartsWith("REQ-", StringComparison.OrdinalIgnoreCase) || term.StartsWith("MPR-", StringComparison.OrdinalIgnoreCase)
                    ? term.Substring(4) : term;
                var idValue = int.TryParse(idText, out var parsedId) ? parsedId : -1;
                var isRepairCode = term.StartsWith("REQ-", StringComparison.OrdinalIgnoreCase);

                query = query.Where(p =>
                    (isRepairCode ? p.MaintenanceRequestId == idValue : p.RequestId == idValue) ||
                    p.ItemName.Contains(term) ||
                    p.RequestedByNavigation.FullName.Contains(term) ||
                    p.MaintenanceRequest.BrokenReport.Machine.MachineName.Contains(term));
            }

            Pagination = new PageWindow(await query.CountAsync(), PageNumber);

            var items = await query
                .OrderBy(p => p.Status == null || p.Status == "Pending" ? 0 : 1)
                .ThenByDescending(p => p.RequestDate)
                .ThenByDescending(p => p.RequestId)
                .Skip(Pagination.Offset)
                .Take(PageWindow.Size)
                .Select(p => new PurchaseRow
                {
                    RequestId = p.RequestId,
                    MaintenanceRequestId = p.MaintenanceRequestId,
                    MachineName = p.MaintenanceRequest.BrokenReport.Machine.MachineName,
                    ItemName = p.ItemName,
                    Unit = p.Unit,
                    Quantity = p.Quantity,
                    RequestedByName = p.RequestedByNavigation.FullName,
                    RequestDate = p.RequestDate,
                    Status = p.Status ?? PurchaseRequestStatus.Pending,
                    Note = p.Note,
                    ResultNote = p.ResultNote,
                    TotalCost = p.TotalCost,
                    HandledByName = p.ApprovedByNavigation != null ? p.ApprovedByNavigation.FullName : null,
                    HandledAt = p.HandledAt
                })
                .ToListAsync();

            foreach (var r in items) r.Status = PurchaseRequestStatus.Normalize(r.Status);
            Rows = items;

            Notices = await Notifier.TakeUnreadAsync(_context, CurrentUserId);

            return Page();
        }

        // ------------------------------------------------------------------
        // Manager: đã mua xong -> nhập kho vật tư -> thông báo nhân viên bảo trì
        // ------------------------------------------------------------------
        public async Task<IActionResult> OnPostPurchasedAsync(int id, decimal? totalCost, string? note)
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

            var error = InputValidator.First(
                InputValidator.Text(note, "Ghi chú mua hàng", 300),
                totalCost is < 0m or > 99_999_999_999m ? "Tổng chi phí không hợp lệ." : null);
            if (error != null) return Fail(error);

            return await ResolveAsync(id, purchased: true, totalCost, note);
        }

        // ------------------------------------------------------------------
        // Manager: từ chối mua (bắt buộc ghi lý do)
        // ------------------------------------------------------------------
        public async Task<IActionResult> OnPostRejectAsync(int id, string? reason)
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

            var error = InputValidator.Text(reason, "Lý do từ chối", 300, required: true);
            if (error != null) return Fail(error);

            return await ResolveAsync(id, purchased: false, null, reason);
        }

        private async Task<IActionResult> ResolveAsync(int id, bool purchased, decimal? totalCost, string? note)
        {
            var pr = await _context.MaterialPurchaseRequests
                .Include(p => p.MaintenanceRequest).ThenInclude(m => m.AssignedToNavigation)
                .FirstOrDefaultAsync(p => p.RequestId == id);
            if (pr == null) return Fail("Không tìm thấy yêu cầu mua vật tư.");

            if (PurchaseRequestStatus.Normalize(pr.Status) != PurchaseRequestStatus.Pending)
                return Fail($"Yêu cầu MPR-{id:D4} đã được xử lý trước đó.");

            var mr = pr.MaintenanceRequest;
            var now = DateTime.Now;
            var cleanNote = InputValidator.Clean(note);
            var itemText = $"{pr.ItemName} ×{pr.Quantity}{(string.IsNullOrEmpty(pr.Unit) ? "" : " " + pr.Unit)}";

            await using var tx = await _context.Database.BeginTransactionAsync();

            pr.ApprovedBy = CurrentUserId;
            pr.HandledAt = now;
            pr.ResultNote = cleanNote;

            if (purchased)
            {
                pr.Status = PurchaseRequestStatus.Purchased;
                pr.TotalCost = totalCost;
            }
            else
            {
                pr.Status = PurchaseRequestStatus.Rejected;
            }

            // Còn yêu cầu mua nào của cùng yêu cầu sửa chữa đang chờ không?
            var stillPending = await _context.MaterialPurchaseRequests
                .CountAsync(p => p.MaintenanceRequestId == mr.RequestId && p.RequestId != id && (p.Status == null || p.Status == "Pending"));

            var before = RepairStatus.Normalize(mr.Status);
            var resumed = before == RepairStatus.WaitingMaterial && stillPending == 0;
            if (resumed) mr.Status = RepairStatus.InProgress;

            AddLog(mr, purchased ? RepairAction.MaterialsPurchased : RepairAction.PurchaseRejected,
                before, before,
                Clip((purchased ? "Đã mua " : "Từ chối mua ") + itemText + (cleanNote != null ? ". " + (purchased ? "Ghi chú: " : "Lý do: ") + cleanNote : "")), now);
            if (resumed)
                AddLog(mr, RepairAction.MaterialsReady, RepairStatus.WaitingMaterial, RepairStatus.InProgress,
                    "Các yêu cầu mua vật tư đã được xử lý. Nhân viên bảo trì kiểm tra và xuất vật tư để tiếp tục sửa.", now);

            if (mr.AssignedTo is int technicianId)
            {
                var code = $"REQ-{mr.RequestId:D4}";
                string title, message;
                if (purchased)
                {
                    title = resumed ? "Vật tư đã mua xong – tiếp tục sửa chữa" : "Một phần vật tư đã được mua";
                    message = resumed
                        ? $"Quản lý đã mua {itemText} cho {code}. Hãy vào kiểm tra & xuất vật tư rồi tiếp tục sửa chữa."
                        : $"Quản lý đã mua {itemText} cho {code}. Còn {stillPending} yêu cầu mua khác đang chờ.";
                }
                else
                {
                    title = "Yêu cầu mua vật tư bị từ chối";
                    message = $"Quản lý từ chối mua {itemText} cho {code}. Lý do: {cleanNote}." +
                              (resumed ? " Yêu cầu quay lại trạng thái sửa chữa, bạn có thể điều chỉnh vật tư hoặc gửi yêu cầu mới." : "");
                }
                Notifier.Add(_context, technicianId, title, message, $"/Maintenance/Details?id={mr.RequestId}");
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            SuccessMessage = purchased
                ? $"Đã ghi nhận mua {itemText}." + (resumed ? $" {mr.AssignedToNavigation?.FullName ?? "Nhân viên bảo trì"} được thông báo để tiếp tục sửa chữa REQ-{mr.RequestId:D4}." : $" Còn {stillPending} yêu cầu mua khác của REQ-{mr.RequestId:D4} đang chờ.")
                : $"Đã từ chối mua {itemText}." + (resumed ? " Yêu cầu sửa chữa đã quay lại trạng thái đang sửa." : "");
            return RedirectToPage(new { StatusFilter });
        }

        private IActionResult Fail(string message)
        {
            ErrorMessage = message;
            return RedirectToPage(new { StatusFilter });
        }

        private void AddLog(MaintenanceRequest request, string action, string? from, string? to, string? note, DateTime at)
        {
            request.Logs.Add(new MaintenanceRequestLog
            {
                Action = action,
                FromStatus = from,
                ToStatus = to,
                ActorId = CurrentUserId,
                Note = note,
                CreatedAt = at
            });
        }

        private static string Clip(string text) => text.Length <= 500 ? text : text.Substring(0, 499) + "…";
    }
}
