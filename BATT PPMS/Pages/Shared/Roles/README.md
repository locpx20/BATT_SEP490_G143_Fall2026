# Giao diện theo vai trò

Mỗi vai trò có một thư mục giao diện độc lập:

| Thư mục | Vai trò trong hệ thống |
| --- | --- |
| Admin | Admin System / Admin |
| Owner | Owner |
| Manager | Manager |
| Purchasing | Purchasing Staff |
| Warehouse | Warehouse Staff |
| Production | Staff |
| QualityControl | QC Staff |
| Maintenance | Maintenance Staff |

Trong mỗi thư mục:

- `_Navigation.cshtml`: menu sidebar của role.
- `_Topbar.cshtml`: avatar, tài khoản, thông báo và các nút thanh trên của role.
- `_Dashboard.cshtml`: nội dung trang tổng quan của role.

Riêng Admin có `_AccountMenu.cshtml` cho avatar và menu tài khoản ở cuối sidebar. `_Topbar.cshtml` để trống phần nội dung, không hiển thị chuông thông báo.

Muốn thay đổi giao diện của một role, sửa trực tiếp ba file trong thư mục tương ứng.
Owner và Manager có file riêng dù hiện tại hiển thị giống nhau.

`Helpers/RoleViews.cs` ánh xạ tên role sang thư mục bằng danh sách cố định.
`Pages/Shared/_Layout.cshtml` giữ khung trang, logo, tải CSS/JS và gọi menu/thanh trên của role đang đăng nhập.
`Pages/Dashboard/Index.cshtml` gọi trang tổng quan của role với dữ liệu từ PageModel hiện có.
Các file partial không có `@page`, nên không tạo đường dẫn có thể truy cập trực tiếp.

Các màn hình nghiệp vụ (Orders, PurchaseInvoices, Maintenance...), hồ sơ và đổi mật khẩu tiếp tục nằm tại các thư mục Pages hiện có. Chúng dùng chung dữ liệu, xử lý và kiểm tra quyền phía server; không sao chép logic nghiệp vụ sang các file giao diện role.
Màu sắc, phông chữ và CSS dùng chung giữ tại `wwwroot/css`.

Owner dùng `wwwroot/css/owner-workspace.css` riêng, không thay logo chung.
`Owner/_Partners.cshtml` là bảng Nhà cung cấp / Khách hàng của Owner; màn hình các role khác vẫn dùng giao diện nghiệp vụ hiện có.
`Owner/_AccountMenu.cshtml` đặt tài khoản ở cuối sidebar với menu xanh đồng bộ. Owner không hiển thị thanh trên; `_Topbar.cshtml` để trống nội dung.
Các mục công nợ/tài chính và kho trỏ tới phần tổng quan tài chính và trang kho hiện có, không bổ sung quy trình thanh toán hoặc dữ liệu công nợ mới.
