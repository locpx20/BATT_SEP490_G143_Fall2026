using System.ComponentModel.DataAnnotations;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Orders
{
    /// <summary>
    /// Quy tắc workflow của đơn hàng xuất kho (Outbound Order).
    /// Manager chỉ được sửa / xoá khi đơn còn ở trạng thái Pending (chưa được Owner duyệt).
    /// </summary>
    public static class OrderWorkflow
    {
        public static bool CanModify(string? status) => status == StatusConstants.Pending;

        public static string Label(string? status) => status switch
        {
            StatusConstants.Pending => "Chờ duyệt",
            StatusConstants.Approved => "Đã duyệt",
            StatusConstants.Rejected => "Từ chối",
            StatusConstants.Completed => "Hoàn thành",
            StatusConstants.Cancelled => "Đã hủy",
            _ => status ?? "—"
        };

        public static string Badge(string? status) => status switch
        {
            StatusConstants.Pending => "badge-pending",
            StatusConstants.Approved => "badge-approved",
            StatusConstants.Rejected => "badge-rejected",
            StatusConstants.Completed => "badge-completed",
            _ => "badge-inactive"
        };

        public static readonly string[] AllStatuses =
        {
            StatusConstants.Pending, StatusConstants.Approved, StatusConstants.Rejected,
            StatusConstants.Completed, StatusConstants.Cancelled
        };

        /// <summary>Giới hạn của cột decimal(12,2) trong bảng [Order].</summary>
        public const decimal MaxAmount = 9_999_999_999.99m;
    }

    /// <summary>Dữ liệu nhập từ form Create / Update Outbound Order.</summary>
    public class OrderInput
    {
        [Required(ErrorMessage = "Vui lòng chọn khách hàng.")]
        public int? CustomerId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn sản phẩm / hàng hoá.")]
        public int? InventoryId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số lượng.")]
        [Range(1, 1_000_000, ErrorMessage = "Số lượng phải là số nguyên từ 1 đến 1.000.000.")]
        public int? Quantity { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập đơn giá.")]
        [Range(0.01, 9_999_999_999.99, ErrorMessage = "Đơn giá phải lớn hơn 0.")]
        public decimal? UnitPrice { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày đặt hàng.")]
        public DateTime? OrderDate { get; set; }
    }

    /// <summary>
    /// Base chung cho Create và Update: nạp danh sách khách hàng / sản phẩm và validate nghiệp vụ.
    /// </summary>
    public abstract class OrderFormPageModel : BasePageModel
    {
        protected readonly ProductionManagementDbContext _context;

        protected OrderFormPageModel(ProductionManagementDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public OrderInput Input { get; set; } = new();

        public List<Customer> Customers { get; private set; } = new();
        public List<InventoryItem> Items { get; private set; } = new();

        /// <summary>Có phải form sửa đơn (true) hay tạo mới (false).</summary>
        public abstract bool IsEdit { get; }

        public string? EditingOrderCode { get; protected set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Nạp dropdown. Khi sửa đơn, vẫn giữ khách hàng / sản phẩm hiện tại dù đã bị ngưng hoạt động.
        /// </summary>
        protected async Task LoadLookupsAsync(int? keepCustomerId = null, int? keepInventoryId = null)
        {
            Customers = await _context.Customers.AsNoTracking()
                .Where(c => c.Status == StatusConstants.Active || c.CustomerId == keepCustomerId)
                .OrderBy(c => c.CustomerName)
                .ToListAsync();

            Items = await _context.InventoryItems.AsNoTracking()
                .Where(i => i.Status == "Available" || i.InventoryId == keepInventoryId)
                .OrderBy(i => i.ItemName)
                .ToListAsync();
        }

        /// <summary>
        /// Kiểm tra nghiệp vụ ngoài DataAnnotations: khách hàng hợp lệ, đủ tồn kho, tổng tiền không tràn cột.
        /// Lỗi được thêm vào ModelState.
        /// </summary>
        protected async Task ValidateBusinessRulesAsync(int? keepCustomerId = null, int? keepInventoryId = null)
        {
            if (!ModelState.IsValid) return;

            var customer = await _context.Customers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CustomerId == Input.CustomerId);
            if (customer == null ||
                (customer.Status != StatusConstants.Active && customer.CustomerId != keepCustomerId))
            {
                ModelState.AddModelError("Input.CustomerId", "Khách hàng không tồn tại hoặc đã ngưng hoạt động.");
            }

            var item = await _context.InventoryItems.AsNoTracking()
                .FirstOrDefaultAsync(i => i.InventoryId == Input.InventoryId);
            if (item == null)
            {
                ModelState.AddModelError("Input.InventoryId", "Sản phẩm không tồn tại trong kho.");
            }
            else
            {
                if (item.Status != "Available" && item.InventoryId != keepInventoryId)
                    ModelState.AddModelError("Input.InventoryId", "Sản phẩm hiện không khả dụng để xuất kho.");

                if (Input.Quantity > item.Quantity)
                    ModelState.AddModelError("Input.Quantity",
                        $"Số lượng vượt quá tồn kho hiện tại ({item.Quantity:N0} {item.Unit}).");
            }

            if (Input.Quantity.HasValue && Input.UnitPrice.HasValue &&
                (decimal)Input.Quantity.Value * Input.UnitPrice.Value > OrderWorkflow.MaxAmount)
            {
                ModelState.AddModelError("Input.UnitPrice", "Tổng tiền đơn hàng vượt quá giới hạn cho phép.");
            }
        }
    }
}
