using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.ExportSlips
{
    /// <summary>
    /// Screen: Chi tiết phiếu xuất kho. Thủ kho bấm "Hoàn thành xuất kho" để hệ thống trừ tồn kho từng lô,
    /// chuyển yêu cầu xuất kho sang Đã xuất và yêu cầu bán sang Hoàn thành.
    /// </summary>
    public class DetailModel : BasePageModel
    {
        private readonly ProductionManagementDbContext _context;

        public DetailModel(ProductionManagementDbContext context)
        {
            _context = context;
        }

        public ExportSlip Slip { get; private set; } = null!;

        public bool CanComplete => IsWarehouseStaff && Slip.Status == ExportSlipStatus.Processing;

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var check = RequireRole(RoleConstants.WarehouseStaff, RoleConstants.Manager, RoleConstants.Owner);
            if (check != null) return check;

            var slip = await _context.ExportSlips.AsNoTracking()
                .Include(s => s.CreatedByNavigation)
                .Include(s => s.CompletedByNavigation)
                .Include(s => s.Delivery)
                .Include(s => s.Order).ThenInclude(o => o.Customer)
                .Include(s => s.Lines).ThenInclude(l => l.Batch)
                .AsSplitQuery()
                .FirstOrDefaultAsync(s => s.SlipId == id);
            if (slip == null) return NotFound();

            Slip = slip;
            return Page();
        }

        public async Task<IActionResult> OnPostCompleteAsync(int id)
        {
            var check = RequireRole(RoleConstants.WarehouseStaff);
            if (check != null) return check;

            // Giao dịch: nếu bất kỳ lô nào không đủ hàng thì hủy toàn bộ, không trừ kho dở dang.
            // Dispose mà chưa Commit sẽ tự Rollback.
            await using var tx = await _context.Database.BeginTransactionAsync();

            // Chiếm phiếu một cách nguyên tử để bấm hai lần / hai thủ kho cùng lúc không trừ kho hai lần.
            var claimed = await _context.ExportSlips
                .Where(s => s.SlipId == id && s.Status == ExportSlipStatus.Processing)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ExportSlipStatus.Completed));
            if (claimed == 0)
                return Fail(id, "Phiếu xuất kho không tồn tại hoặc đã được hoàn thành.");

            var slip = await _context.ExportSlips
                .Include(s => s.Lines).ThenInclude(l => l.Batch)
                .Include(s => s.Delivery)
                .Include(s => s.Order)
                .AsSplitQuery()
                .FirstAsync(s => s.SlipId == id);

            // Trừ tồn kho từng lô; điều kiện Quantity >= số xuất đảm bảo không âm kể cả khi có thao tác song song.
            foreach (var line in slip.Lines)
            {
                var quantity = line.Quantity;
                var batchId = line.BatchId;
                var updated = await _context.FinishedGoodBatches
                    .Where(b => b.BatchId == batchId && b.Status == "In Stock" && b.Quantity >= quantity)
                    .ExecuteUpdateAsync(b => b.SetProperty(x => x.Quantity, x => x.Quantity - quantity));
                if (updated == 0)
                    return Fail(id, $"Lô {line.Batch.BagNumber} không còn đủ {SalesFlow.Bags(quantity)} bao để xuất. Phiếu chưa được hoàn thành, tồn kho không thay đổi.");
            }

            var batchIds = slip.Lines.Select(l => l.BatchId).ToList();
            await _context.FinishedGoodBatches
                .Where(b => batchIds.Contains(b.BatchId) && b.Quantity <= 0)
                .ExecuteUpdateAsync(b => b.SetProperty(x => x.Status, "Depleted"));

            var now = DateTime.Now;
            slip.CompletedBy = CurrentUserId;
            slip.CompletedAt = now;
            slip.Delivery.DeliveryStatus = StatusConstants.Delivered;
            slip.Delivery.ConfirmedBy = CurrentUserId;
            slip.Delivery.UpdatedAt = now;

            // Tính tổng số lượng kg đã xuất của tất cả các phiếu thuộc đơn hàng này (bao gồm cả phiếu vừa hoàn thành)
            var totalExportedKg = await _context.ExportSlips
                .Where(s => s.OrderId == slip.OrderId && s.Status == ExportSlipStatus.Completed)
                .SelectMany(s => s.Lines)
                .SumAsync(l => l.Quantity);

            if (totalExportedKg >= slip.Order.Quantity)
            {
                slip.Order.Status = StatusConstants.Completed;
            }

            var slipBags = slip.Lines.Sum(l => l.Bags);
            var orderTotalBags = (int)Math.Round(slip.Order.Quantity / (decimal)SalesFlow.KgPerBag);
            var exportedBags = (int)Math.Round(totalExportedKg / (decimal)SalesFlow.KgPerBag);
            var remainingBags = Math.Max(0, orderTotalBags - exportedBags);

            var message = slip.Order.Status == StatusConstants.Completed
                ? $"{slip.Order.OrderCode}: Xe {slip.Delivery.LicensePlate} đã hoàn thành xuất {slipBags:N0} bao. Đơn hàng đã xuất đủ {orderTotalBags:N0} bao và hoàn thành!"
                : $"{slip.Order.OrderCode}: Xe {slip.Delivery.LicensePlate} đã hoàn thành xuất {slipBags:N0} bao. Đơn hàng còn lại {remainingBags:N0} bao cần xuất.";

            await SalesFlow.NotifyUserOrRoleAsync(_context, slip.Delivery.CreatedBy ?? slip.Order.CreatedBy, RoleConstants.Manager,
                "Đã hoàn thành xuất kho", message, $"/ExportSlips/Detail?id={slip.SlipId}");
            await SalesFlow.NotifyRoleAsync(_context, RoleConstants.Owner,
                "Đã hoàn thành xuất kho", message, $"/ExportSlips/Detail?id={slip.SlipId}");

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DataValidationException ex)
            {
                return Fail(id, string.Join(" ", ex.Errors));
            }

            await tx.CommitAsync();

            SuccessMessage = $"Đã hoàn thành phiếu {slip.SlipCode}; kho thành phẩm đã được cập nhật.";
            return RedirectToPage(new { id });
        }

        private IActionResult Fail(int id, string message)
        {
            ErrorMessage = message;
            return RedirectToPage(new { id });
        }
    }
}
