using System.ComponentModel.DataAnnotations;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Orders
{
    /// <summary>
    /// Quy tắc workflow của yêu cầu bán hàng (Order):
    /// Pending (Manager tạo) -> Approved (Owner gán khách + giá) | Rejected (Owner ghi lý do)
    /// Approved -> Completed khi phiếu xuất kho hoàn thành. Manager chỉ sửa / hủy khi Pending hoặc Rejected.
    /// </summary>
    public static class OrderWorkflow
    {
        public static bool CanModify(string? status) => status is StatusConstants.Pending or StatusConstants.Rejected;

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

        /// <summary>Yêu cầu xuất kho còn hiệu lực của đơn (bỏ qua yêu cầu đã hủy). Cần Include(Deliveries).</summary>
        public static Delivery? ActiveDelivery(Order order) => order.Deliveries
            .Where(d => d.DeliveryStatus != StatusConstants.Cancelled)
            .OrderByDescending(d => d.DeliveryId)
            .FirstOrDefault();

        /// <summary>Tiến độ chi tiết hiển thị ở danh sách (cần Include Deliveries + ExportSlip).</summary>
        public static (string Label, string Badge) Stage(Order order)
        {
            switch (order.Status)
            {
                case StatusConstants.Pending: return ("Chờ Owner duyệt", "badge-pending");
                case StatusConstants.Rejected: return ("Owner từ chối", "badge-rejected");
                case StatusConstants.Cancelled: return ("Đã hủy", "badge-inactive");
                case StatusConstants.Completed: return ("Đã xuất kho", "badge-completed");
                case StatusConstants.Approved:
                    var active = order.Deliveries.Where(d => d.DeliveryStatus != StatusConstants.Cancelled).ToList();
                    if (active.Count == 0) return ("Chờ tạo xe lấy hàng", "badge-approved");
                    if (active.Any(d => d.DeliveryStatus == "Returned")) return ("Xe bị trả lại – cần sửa", "badge-rejected");
                    if (active.Any(d => d.DeliveryStatus == StatusConstants.Prepared && d.ExportSlips.Count == 0)) return ("Có xe chờ kiểm tra", "badge-pending");
                    // Đơn còn Approved nghĩa là chưa xuất đủ: đã có phiếu hoàn thành thì là xuất một phần.
                    if (active.Any(d => d.ExportSlips.Any(s => s.Status == ExportSlipStatus.Completed))) return ("Đã xuất một phần", "badge-medium");
                    if (active.Any(d => d.ExportSlips.Any(s => s.Status == ExportSlipStatus.Processing))) return ("Đang lập phiếu xuất", "badge-medium");
                    return ("Chờ lập phiếu xuất", "badge-pending");
                default: return (Label(order.Status), Badge(order.Status));
            }
        }

        /// <summary>Giới hạn của cột decimal(12,2) trong bảng [Order].</summary>
        public const decimal MaxAmount = 9_999_999_999.99m;

        public const int MaxBagsPerOrder = 10_000;
    }

    /// <summary>Quy tắc workflow của yêu cầu xuất kho (Delivery) và phiếu xuất kho.</summary>
    public static class DeliveryWorkflow
    {
        /// <summary>Manager chỉ sửa được khi thủ kho chưa lập phiếu (Prepared / Returned).</summary>
        public static bool CanEdit(Delivery d) =>
            d.DeliveryStatus is StatusConstants.Prepared or "Returned" && d.ExportSlips.Count == 0;

        public static string Label(string? status) => status switch
        {
            StatusConstants.Prepared => "Chờ thủ kho kiểm tra",
            "Returned" => "Thủ kho trả lại – cần sửa",
            StatusConstants.InTransit => "Đang vận chuyển",
            StatusConstants.Delivered => "Đã xuất",
            StatusConstants.Cancelled => "Đã hủy",
            _ => status ?? "—"
        };

        public static string Badge(string? status) => status switch
        {
            StatusConstants.Prepared => "badge-pending",
            "Returned" => "badge-rejected",
            StatusConstants.InTransit or StatusConstants.Delivered => "badge-completed",
            _ => "badge-inactive"
        };

        /// <summary>Xe đã xuất nhưng đơn chưa xuất đủ thì hiển thị "Đã xuất một phần"; chỉ khi đơn Completed mới là "Đã xuất kho".</summary>
        public static (string Label, string Badge) Stage(Delivery d, Order? order = null)
        {
            if (d.ExportSlips.Any(s => s.Status == ExportSlipStatus.Processing))
                return ("Đang lập phiếu xuất kho", "badge-medium");
            if (d.DeliveryStatus == StatusConstants.Delivered)
            {
                var status = (order ?? d.Order)?.Status;
                return status == StatusConstants.Completed
                    ? ("Đã xuất kho", "badge-completed")
                    : ("Đã xuất một phần", "badge-medium");
            }
            return (Label(d.DeliveryStatus), Badge(d.DeliveryStatus));
        }

        public static string SlipLabel(string? status) => status switch
        {
            ExportSlipStatus.Processing => "Đang xử lý",
            ExportSlipStatus.Completed => "Đã hoàn thành",
            _ => status ?? "—"
        };

        public static string SlipBadge(string? status) =>
            status == ExportSlipStatus.Completed ? "badge-completed" : "badge-medium";
    }

    /// <summary>Dữ liệu nhập từ form tạo / sửa yêu cầu bán hàng: chỉ cần loại và số bao.</summary>
    public class OrderInput
    {
        [Required(ErrorMessage = "Vui lòng chọn loại thành phẩm (A hoặc B).")]
        public string? Grade { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số bao cần bán.")]
        [Range(1, OrderWorkflow.MaxBagsPerOrder, ErrorMessage = "Số bao phải là số nguyên từ 1 đến 10.000.")]
        public int? Bags { get; set; }

        [StringLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự.")]
        public string? Note { get; set; }
    }

    /// <summary>Base chung cho Create và Edit: nạp lô khả dụng, kiểm tra tồn kho và phân bổ lô theo FIFO.</summary>
    public abstract class OrderFormPageModel : BasePageModel
    {
        protected readonly ProductionManagementDbContext _context;

        protected OrderFormPageModel(ProductionManagementDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public OrderInput Input { get; set; } = new();

        /// <summary>Lô còn bán được theo từng loại, sắp xếp FIFO (dùng để xem trước phân bổ).</summary>
        public Dictionary<string, List<LotAvailability>> LotsByGrade { get; private set; } = new();

        public abstract bool IsEdit { get; }

        public string? EditingOrderCode { get; protected set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        protected async Task LoadLotsAsync(int? excludeOrderId = null)
        {
            LotsByGrade = new();
            foreach (var grade in SalesFlow.Grades)
                LotsByGrade[grade] = await SalesFlow.AvailableLotsAsync(_context, grade, excludeOrderId, HttpContext.RequestAborted);
        }

        /// <summary>
        /// Kiểm tra đủ hàng và phân bổ lô theo FIFO. Lỗi được thêm vào ModelState; trả null nếu không hợp lệ.
        /// </summary>
        protected async Task<List<LotAllocation>?> AllocateAsync(int? excludeOrderId = null)
        {
            if (!ModelState.IsValid) return null;
            if (!SalesFlow.IsGrade(Input.Grade))
            {
                ModelState.AddModelError("Input.Grade", "Loại thành phẩm không hợp lệ.");
                return null;
            }

            var lots = await SalesFlow.AvailableLotsAsync(_context, Input.Grade, excludeOrderId, HttpContext.RequestAborted);
            var allocation = SalesFlow.Allocate(lots, Input.Bags!.Value);
            if (allocation == null)
            {
                var available = lots.Sum(l => l.AvailableBags);
                ModelState.AddModelError("Input.Bags",
                    $"Kho {SalesFlow.GradeLabel(Input.Grade)} chỉ còn {available:N0} bao khả dụng " +
                    $"(đã trừ phần giữ chỗ cho các yêu cầu bán khác), không đủ {Input.Bags.Value:N0} bao.");
            }
            return allocation;
        }

        protected static void ApplyAllocation(Order order, List<LotAllocation> allocation)
        {
            foreach (var line in allocation)
                order.OrderDetails.Add(new OrderDetail { BatchId = line.BatchId, Quantity = line.Kg });
        }
    }
}
