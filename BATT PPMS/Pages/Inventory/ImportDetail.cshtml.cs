using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Inventory
{
    public class ImportDetailModel : BasePageModel
    {
        private readonly AppDbContext _context;

        public ImportDetailModel(AppDbContext context)
        {
            _context = context;
        }

        public RawMaterialImport Import { get; private set; } = null!;
        public string ImportCode { get; private set; } = string.Empty;
        public string ReceiptCode { get; private set; } = "—";
        public int? InvoiceId { get; private set; }
        public string InvoiceCode { get; private set; } = "—";
        public string MaterialName { get; private set; } = "Nguyên liệu thô";
        public string MaterialUnit { get; private set; } = "kg";
        public string SupplierName { get; private set; } = "—";
        public string SupplierPhone { get; private set; } = "—";
        public string SupplierAddress { get; private set; } = "—";
        public string DriverName { get; private set; } = "—";
        public string LicensePlate { get; private set; } = "—";
        public string DeliveryTime { get; private set; } = "—";
        public string ImportedByName { get; private set; } = "—";
        public string PurchasingStaffName { get; private set; } = "—";
        public string ApprovedByName { get; private set; } = string.Empty;
        public DateTime? ApprovedAt { get; private set; }
        public decimal TotalAmount => Import.Quantity * Import.UnitPrice;
        public string StatusText { get; private set; } = "Chờ duyệt";
        public string StatusBadgeClass { get; private set; } = "badge-pending";
        public string? ScalePhotoPath { get; private set; }
        public string? InvoicePhotoPath { get; private set; }

        public bool CanManagerApprove => IsManager && string.Equals(Import.Status, "Pending", StringComparison.OrdinalIgnoreCase);

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id, string? code)
        {
            var check = RequireRole(
                RoleConstants.WarehouseStaff,
                RoleConstants.Manager,
                RoleConstants.Owner,
                RoleConstants.PurchasingStaff);
            if (check != null) return check;

            var query = _context.RawMaterialImports
                .AsNoTracking()
                .Include(i => i.Material)
                .Include(i => i.Supplier)
                .Include(i => i.ImportedByNavigation)
                .Include(i => i.ApprovedByNavigation)
                .Include(i => i.Receipt)
                    .ThenInclude(r => r!.CreatedByNavigation)
                .Include(i => i.Receipt)
                    .ThenInclude(r => r!.Invoice);

            RawMaterialImport? imp = null;
            if (id.HasValue && id.Value > 0)
            {
                imp = await query.FirstOrDefaultAsync(i => i.ImportId == id.Value);
            }
            else if (!string.IsNullOrWhiteSpace(code))
            {
                var cleanCode = code.Trim();
                imp = await query.FirstOrDefaultAsync(i => i.ImportCode == cleanCode);
            }

            if (imp == null) return NotFound();

            Import = imp;
            ImportCode = string.IsNullOrWhiteSpace(imp.ImportCode) ? $"PNK-26{imp.ImportId:D3}" : imp.ImportCode!;
            ReceiptCode = imp.Receipt?.ReceiptCode ?? "—";
            InvoiceId = imp.Receipt?.InvoiceId;
            InvoiceCode = imp.Receipt?.Invoice != null
                ? BATT_PPMS.Pages.PurchaseInvoices.IndexModel.FormatInvoiceBadgeCode(imp.Receipt.Invoice.InvoiceCode, imp.Receipt.Invoice.InvoiceId)
                : "—";

            MaterialName = imp.Material?.MaterialName ?? "Nguyên liệu thô";
            MaterialUnit = string.IsNullOrWhiteSpace(imp.Material?.Unit) ? "kg" : imp.Material!.Unit!;
            SupplierName = imp.Supplier?.SupplierName ?? "—";
            SupplierPhone = string.IsNullOrWhiteSpace(imp.Supplier?.Phone) ? "—" : imp.Supplier!.Phone!;
            SupplierAddress = string.IsNullOrWhiteSpace(imp.Supplier?.Address) ? "—" : imp.Supplier!.Address!;

            if (imp.Receipt != null)
            {
                var delivery = NoteTags.GetInboundDelivery(imp.Receipt.Invoice?.Note, imp.Receipt.ReceiptId);
                if (delivery != null)
                {
                    LicensePlate = string.IsNullOrWhiteSpace(delivery.LicensePlate) ? "—" : delivery.LicensePlate;
                    DriverName = string.IsNullOrWhiteSpace(delivery.DriverName) ? "—" : delivery.DriverName;
                    DeliveryTime = string.IsNullOrWhiteSpace(delivery.DeliveryTime) ? "—" : delivery.DeliveryTime;
                }
                PurchasingStaffName = imp.Receipt.CreatedByNavigation?.FullName ?? "—";
                ScalePhotoPath = imp.Receipt.ScalePhotoPath;
                InvoicePhotoPath = imp.Receipt.InvoicePhotoPath;
            }

            ImportedByName = imp.ImportedByNavigation?.FullName ?? "—";
            ApprovedByName = imp.ApprovedByNavigation?.FullName ?? string.Empty;
            ApprovedAt = imp.ApprovedAt;

            var st = (imp.Status ?? "Pending").Trim();
            (StatusText, StatusBadgeClass) = st switch
            {
                "Approved" => ("Đã duyệt nhập kho", "badge-completed"),
                "Rejected" => ("Từ chối PNK", "badge-rejected"),
                _ => ("Chờ Manager duyệt", "badge-pending")
            };

            return Page();
        }

        public async Task<IActionResult> OnPostApproveAsync(int id)
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

            var importTicket = await _context.RawMaterialImports
                .Include(i => i.Receipt)
                    .ThenInclude(r => r!.Invoice)
                .FirstOrDefaultAsync(i => i.ImportId == id);

            if (importTicket == null) return NotFound();

            var codeDisplay = string.IsNullOrWhiteSpace(importTicket.ImportCode)
                ? $"PNK-26{importTicket.ImportId:D3}"
                : importTicket.ImportCode!;

            if (importTicket.Status == "Approved")
            {
                ErrorMessage = $"Phiếu nhập kho {codeDisplay} đã được phê duyệt trước đó.";
                return RedirectToPage(new { id });
            }

            importTicket.Status = "Approved";
            importTicket.ApprovedBy = CurrentUserId;
            importTicket.ApprovedAt = DateTime.Now;
            importTicket.Note = $"Đã được phê duyệt nhập kho bởi {CurrentFullName} lúc {DateTime.Now:HH:mm dd/MM/yyyy}";

            if (importTicket.Receipt != null)
            {
                importTicket.Receipt.Status = "Received";
                importTicket.Receipt.RejectionReason = null;

                var notifyStaffIds = new HashSet<int>();
                if (importTicket.Receipt.CreatedBy.HasValue) notifyStaffIds.Add(importTicket.Receipt.CreatedBy.Value);
                if (importTicket.Receipt.Invoice?.AssignedTo != null) notifyStaffIds.Add(importTicket.Receipt.Invoice.AssignedTo.Value);

                foreach (var staffId in notifyStaffIds)
                {
                    Notifier.Add(
                        _context,
                        staffId,
                        $"Hoàn tất nhập kho nguyên liệu ({codeDisplay})",
                        $"Manager ({CurrentFullName}) đã duyệt Phiếu nhập kho {codeDisplay} (từ đơn mua {importTicket.Receipt.ReceiptCode}, khối lượng +{importTicket.Quantity:N0} kg).",
                        $"/PurchaseInvoices/Detail?id={importTicket.Receipt.InvoiceId}");
                }
            }

            var material = await _context.RawMaterials.FirstOrDefaultAsync(m => m.MaterialId == importTicket.MaterialId)
                           ?? await _context.RawMaterials.FirstOrDefaultAsync();
            if (material != null)
            {
                material.CurrentQuantity += importTicket.Quantity;
            }

            await _context.SaveChangesAsync();
            await BATT_PPMS.Pages.PurchaseInvoices.IndexModel.ExpirePastDayInvoicesAsync(_context);

            SuccessMessage = $"Đã phê duyệt Phiếu nhập kho nguyên liệu {codeDisplay} và cộng +{importTicket.Quantity:N0} kg vào tồn kho.";
            return RedirectToPage(new { id });
        }
    }
}

