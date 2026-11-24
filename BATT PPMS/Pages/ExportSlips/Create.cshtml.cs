using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using BATT_PPMS.Pages.Orders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.ExportSlips
{
    public class SlipLineItemInput
    {
        public int BatchId { get; set; }
        public int Bags { get; set; }
    }

    public class CreateSlipInput
    {
        public List<SlipLineItemInput> Lines { get; set; } = new();
        public string? Note { get; set; }
    }

    public class AvailableBatchItem
    {
        public int BatchId { get; set; }
        public string BagNumber { get; set; } = "";
        public DateOnly ProductionDate { get; set; }
        public decimal CurrentStockKg { get; set; }
        public int CurrentStockBags => (int)Math.Floor(CurrentStockKg / 800m);
        public bool IsAllocatedToOrder { get; set; }
        public int SuggestedBags { get; set; }
    }

    /// <summary>
    /// Screen: Thủ kho lập phiếu xuất kho cho xe đến lấy hàng. Manager chỉ khai báo tài xế/xe; số bao do thủ kho nhập
    /// ở đây. Một xe có thể có nhiều phiếu cho đến khi đủ số bao của đơn bán.
    /// </summary>
    public class CreateModel : BasePageModel
    {
        private const long MaxImageBytes = 10 * 1024 * 1024;
        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

        private readonly ProductionManagementDbContext _context;
        private readonly IWebHostEnvironment _env;

        public CreateModel(ProductionManagementDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public Delivery Delivery { get; private set; } = null!;
        public Order Order => Delivery.Order;

        public int TotalOrderBags { get; private set; }
        public int ExportedOrderBags { get; private set; }
        /// <summary>Bao đã nằm trong các phiếu đang xử lý (chưa trừ kho) của đơn.</summary>
        public int PendingOrderBags { get; private set; }
        public int RemainingOrderBags { get; private set; }

        /// <summary>Các phiếu đã lập của đơn (mọi xe) để xem nhanh ngay trên trang lập phiếu.</summary>
        public List<ExportSlip> OrderSlips { get; private set; } = new();

        private Dictionary<int, decimal> _reservedKgByBatch = new();
        private Dictionary<int, decimal> _slippedKgByBatch = new();

        public List<AvailableBatchItem> Batches { get; private set; } = new();

        [BindProperty]
        public CreateSlipInput Input { get; set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(int deliveryId)
        {
            var check = RequireRole(RoleConstants.WarehouseStaff);
            if (check != null) return check;

            if (!await LoadDeliveryAsync(deliveryId)) return NotFound();

            var blocked = CheckCanCreateSlip();
            if (blocked != null)
            {
                ErrorMessage = blocked;
                return RedirectToPage("/Deliveries/Detail", new { id = deliveryId });
            }

            await LoadBatchesAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int deliveryId, List<IFormFile>? vehicleImages)
        {
            var check = RequireRole(RoleConstants.WarehouseStaff);
            if (check != null) return check;

            if (!await LoadDeliveryAsync(deliveryId)) return NotFound();

            var blocked = CheckCanCreateSlip();
            if (blocked != null)
            {
                ErrorMessage = blocked;
                return RedirectToPage("/Deliveries/Detail", new { id = deliveryId });
            }

            var selectedLines = Input.Lines.Where(l => l.Bags > 0).ToList();
            if (selectedLines.Count == 0)
            {
                ErrorMessage = "Vui lòng nhập số bao bốc lên xe cho ít nhất một lô thành phẩm.";
                await LoadBatchesAsync();
                return Page();
            }

            // Tổng số bao của phiếu không được vượt phần còn lại của đơn (đã tính cả các phiếu đang xử lý)
            var slipBags = selectedLines.Sum(l => l.Bags);
            if (slipBags > RemainingOrderBags)
            {
                ErrorMessage = $"Phiếu này có {slipBags:N0} bao, vượt quá số bao còn lại của đơn ({RemainingOrderBags:N0} bao).";
                await LoadBatchesAsync();
                return Page();
            }

            // Kiểm tra tồn kho từng lô đã chọn
            var batchIds = selectedLines.Select(l => l.BatchId).Distinct().ToList();
            var dbBatches = await _context.FinishedGoodBatches
                .Where(b => batchIds.Contains(b.BatchId))
                .ToDictionaryAsync(b => b.BatchId);

            foreach (var line in selectedLines)
            {
                if (!dbBatches.TryGetValue(line.BatchId, out var batch))
                {
                    ErrorMessage = $"Không tìm thấy thông tin lô thành phẩm (ID {line.BatchId}).";
                    await LoadBatchesAsync();
                    return Page();
                }

                if (batch.Status != "In Stock")
                {
                    ErrorMessage = $"Lô {batch.BagNumber} không còn trong kho (trạng thái: {batch.Status}).";
                    await LoadBatchesAsync();
                    return Page();
                }

                var requiredKg = line.Bags * 800m;
                var availableKg = batch.Quantity - _reservedKgByBatch.GetValueOrDefault(batch.BatchId);
                if (availableKg < requiredKg)
                {
                    ErrorMessage = $"Lô {batch.BagNumber} chỉ còn {SalesFlow.Bags(Math.Max(0, availableKg))} bao khả dụng (đã trừ phiếu đang xử lý), không đủ xuất {line.Bags} bao.";
                    await LoadBatchesAsync();
                    return Page();
                }
            }

            // Xử lý ảnh của xe
            var allImages = vehicleImages?.Where(f => f.Length > 0).ToList() ?? new List<IFormFile>();
            if (allImages.Count > 6)
            {
                ErrorMessage = "Chỉ được tải lên tối đa 6 ảnh của xe.";
                await LoadBatchesAsync();
                return Page();
            }

            foreach (var img in allImages)
            {
                var err = ValidateImage(img);
                if (err != null)
                {
                    ErrorMessage = err;
                    await LoadBatchesAsync();
                    return Page();
                }
            }

            var savedPaths = new List<string>();
            foreach (var img in allImages)
            {
                savedPaths.Add(await SaveImageAsync(img));
            }

            var existingCodes = await _context.ExportSlips.AsNoTracking().Select(s => s.SlipCode).ToListAsync();
            var slip = new ExportSlip
            {
                SlipCode = SalesFlow.NextCode(existingCodes, "PXK"),
                DeliveryId = Delivery.DeliveryId,
                OrderId = Order.OrderId,
                Status = ExportSlipStatus.Processing,
                ImagePath = savedPaths.Count > 0 ? string.Join(";", savedPaths) : null,
                Note = string.IsNullOrWhiteSpace(Input.Note) ? null : Input.Note.Trim(),
                CreatedBy = CurrentUserId,
                CreatedAt = DateTime.Now
            };

            _context.ExportSlips.Add(slip);

            foreach (var line in selectedLines)
            {
                slip.Lines.Add(new ExportSlipLine
                {
                    BatchId = line.BatchId,
                    Grade = Order.Grade,
                    Bags = line.Bags,
                    Quantity = line.Bags * 800m
                });
            }

            await SalesFlow.NotifyUserOrRoleAsync(_context, Delivery.CreatedBy ?? Order.CreatedBy, RoleConstants.Manager,
                "Đã lập phiếu xuất kho cho xe đến lấy",
                $"{Order.OrderCode}: Xe {Delivery.LicensePlate} (tài xế {Delivery.DriverName}) đã được lập phiếu xuất kho {slip.SlipCode} với {slipBags:N0} bao (đơn còn {RemainingOrderBags - slipBags:N0} bao chưa lập phiếu).",
                $"/ExportSlips/Detail?id={slip.SlipId}");

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                DeleteImages(savedPaths);
                ErrorMessage = "Không thể tạo phiếu xuất kho. Vui lòng tải lại trang và thử lại.";
                await LoadBatchesAsync();
                return Page();
            }
            catch (DataValidationException ex)
            {
                DeleteImages(savedPaths);
                ErrorMessage = string.Join(" ", ex.Errors);
                await LoadBatchesAsync();
                return Page();
            }

            SuccessMessage = $"Đã lập phiếu xuất kho {slip.SlipCode} cho xe {Delivery.LicensePlate}.";
            return RedirectToPage("/ExportSlips/Detail", new { id = slip.SlipId });
        }

        private async Task<bool> LoadDeliveryAsync(int deliveryId)
        {
            var delivery = await _context.Deliveries
                .Include(d => d.CreatedByNavigation)
                .Include(d => d.Order).ThenInclude(o => o.Customer)
                .Include(d => d.Order).ThenInclude(o => o.OrderDetails).ThenInclude(od => od.Batch)
                .AsSplitQuery()
                .FirstOrDefaultAsync(d => d.DeliveryId == deliveryId);

            if (delivery == null) return false;

            Delivery = delivery;

            TotalOrderBags = (int)Math.Round(Order.Quantity / 800.0);

            // Gom các dòng phiếu của cả đơn (mọi xe, mọi phiếu) để tính số bao đã xuất / đang chờ / còn lại.
            var orderLines = await _context.ExportSlipLines.AsNoTracking()
                .Where(l => l.Slip.OrderId == delivery.OrderId)
                .Select(l => new { l.BatchId, l.Bags, l.Quantity, l.Slip.Status })
                .ToListAsync();

            ExportedOrderBags = orderLines.Where(l => l.Status == ExportSlipStatus.Completed).Sum(l => l.Bags);
            PendingOrderBags = orderLines.Where(l => l.Status == ExportSlipStatus.Processing).Sum(l => l.Bags);
            RemainingOrderBags = Math.Max(0, TotalOrderBags - ExportedOrderBags - PendingOrderBags);

            OrderSlips = await _context.ExportSlips.AsNoTracking()
                .Include(s => s.Delivery)
                .Include(s => s.Lines).ThenInclude(l => l.Batch)
                .Where(s => s.OrderId == delivery.OrderId)
                .OrderBy(s => s.CreatedAt).ThenBy(s => s.SlipId)
                .AsSplitQuery()
                .ToListAsync();

            _reservedKgByBatch = orderLines.Where(l => l.Status == ExportSlipStatus.Processing)
                .GroupBy(l => l.BatchId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            _slippedKgByBatch = orderLines.GroupBy(l => l.BatchId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            return true;
        }

        /// <summary>Phiếu chỉ lập được khi xe còn hiệu lực, đơn còn Approved và đơn còn bao chưa lập phiếu.</summary>
        private string? CheckCanCreateSlip()
        {
            if (Order.Status != StatusConstants.Approved || Delivery.DeliveryStatus is not (StatusConstants.Prepared or StatusConstants.Delivered))
                return "Yêu cầu xuất kho này không ở trạng thái sẵn sàng để lập phiếu.";
            if (RemainingOrderBags <= 0)
                return "Đơn hàng đã được lập phiếu đủ số bao, không thể lập thêm phiếu.";
            return null;
        }

        private async Task LoadBatchesAsync()
        {
            var allocatedBatchIds = Order.OrderDetails.Select(od => od.BatchId).ToHashSet();
            // Gợi ý = phần phân bổ FIFO của lô trừ số đã nằm trong các phiếu trước của đơn
            var allocatedDict = Order.OrderDetails.ToDictionary(od => od.BatchId,
                od => Math.Max(0, (int)Math.Round((od.Quantity - _slippedKgByBatch.GetValueOrDefault(od.BatchId)) / 800m)));

            // Lấy tất cả các lô có sẵn trong kho của loại sản phẩm tương ứng
            var gradedQuery = SalesFlow.InStockGradedLots(_context);
            if (!string.IsNullOrEmpty(Order.Grade))
            {
                gradedQuery = gradedQuery.Where(l => l.Grade == Order.Grade);
            }

            var inStockList = await gradedQuery.ToListAsync();

            Batches = inStockList
                .Select(b => new AvailableBatchItem
                {
                    BatchId = b.BatchId,
                    BagNumber = b.Lot,
                    ProductionDate = b.ProductionDate,
                    CurrentStockKg = Math.Max(0, b.StockKg - _reservedKgByBatch.GetValueOrDefault(b.BatchId)),
                    IsAllocatedToOrder = allocatedBatchIds.Contains(b.BatchId),
                    SuggestedBags = allocatedDict.GetValueOrDefault(b.BatchId, 0)
                })
                .OrderByDescending(b => b.IsAllocatedToOrder)
                .ThenBy(b => b.ProductionDate)
                .ThenBy(b => b.BatchId)
                .ToList();
        }

        private static string? ValidateImage(IFormFile? file)
        {
            if (file == null || file.Length == 0) return null;
            if (file.Length > MaxImageBytes) return $"Ảnh \"{file.FileName}\" không được vượt quá 10 MB.";
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedImageExtensions.Contains(ext) || !(file.ContentType?.StartsWith("image/") ?? false))
                return $"Ảnh \"{file.FileName}\" phải là tệp JPG, PNG hoặc WEBP.";
            return null;
        }

        private async Task<string> SaveImageAsync(IFormFile file)
        {
            var dir = Path.Combine(_env.WebRootPath, "uploads", "export-slips");
            Directory.CreateDirectory(dir);
            var shortId = Guid.NewGuid().ToString("N")[..12];
            var name = $"{shortId}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
            await using (var stream = System.IO.File.Create(Path.Combine(dir, name)))
                await file.CopyToAsync(stream);
            return $"/uploads/export-slips/{name}";
        }

        private void DeleteImages(IEnumerable<string>? relativePaths)
        {
            if (relativePaths == null) return;
            foreach (var path in relativePaths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                var full = Path.Combine(_env.WebRootPath, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
            }
        }
    }
}
