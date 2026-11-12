# BATT_SEP490_G143_Fall2026

## Cập nhật database sau khi pull luồng bảo trì

Với database đang có dữ liệu, chạy `Add_MaintenanceRequest_Workflow.sql` trước, rồi chạy `Add_MaintenanceMaterialFlow.sql` trên database ứng dụng đang sử dụng. Hai script bổ sung các cột bảo trì, nhật ký và thông báo còn thiếu, có thể chạy lại. Không cần chạy lại file tạo database nền. Nếu gặp `Invalid column name` với `due_date`, `location`, `machine_stopped`, `rejection_reason` hoặc `title`, database chưa được cập nhật phần cấu trúc bảo trì.

## Báo cáo cuối ca: hai luồng độc lập

Mã lô thành phẩm dùng chung định dạng của main: `LOT-yyyyMMdd-<25 ký tự hex ngẫu nhiên>` (38 ký tự), lấy ngày sản xuất của phân công. Mã được tạo một lần khi Staff gửi yêu cầu QC, lưu tại `FinishedGoodBatch.bag_number`; QC, yêu cầu nhập kho, tồn kho, đơn hàng và thông báo đọc lại cùng mã này. Dashboard hiển thị mã từ phiếu nhập thành phẩm thực tế, không dùng mã LOT mẫu. Dữ liệu mẫu mới cũng theo định dạng này; các mã đã lưu trước đây được giữ để tra cứu lịch sử. Không cần chạy lại database hay dữ liệu mẫu để áp dụng thay đổi mã cho lô mới.

Với database đang có dữ liệu, chạy lần lượt các script còn thiếu theo thứ tự `Add_Production_QC_Warehouse_Workflow.sql`, `Add_ShiftMaterialUsage.sql`, `Add_MaterialUsageReview.sql`, `Add_QC_ResultImage.sql` và `Add_DailyMaterialPlan.sql`. Sau đó chạy bản mới của `Add_ShiftProductionPlan.sql` để bổ sung trạng thái ca nghỉ, kể cả khi đã chạy bản trước. Script giữ các phân công đã lưu. Khởi động lại ứng dụng sau khi cập nhật.

Menu Staff “Báo cáo cuối ca làm việc” có hai mục:

1. **Nguyên liệu tiêu thụ trong ca làm việc**: Staff chọn phân công đã đến ngày sản xuất, nhập một khối lượng nguyên liệu thực tế đã dùng theo kg, gửi Thủ kho. Không cần QC hoặc Manager duyệt. Thủ kho từ chối phải nhập lý do; Staff sửa và gửi lại. Thủ kho đồng ý mới tạo phiếu tiêu hao và trừ tồn trong cùng giao dịch. Các dòng kho cùng tên nguyên liệu, đơn vị kg được tính tổng; khi duyệt, hệ thống trừ lần lượt theo mã nguyên liệu tăng dần và giữ liên kết phiếu xuất ở từng dòng. Một Staff chỉ khai báo một lần mỗi ngày/ca; phiếu đã duyệt không được sửa và không trừ tồn lần nữa.
2. **Yêu cầu nhập kho thành phẩm**: Staff yêu cầu QC kiểm tra lô → QC tiếp nhận → nhập kết quả và ảnh bắt buộc → Staff gửi yêu cầu nhập kho cho Thủ kho khi QC đạt. Thủ kho có thể từ chối kèm lý do để Staff sửa ghi chú, gửi lại. Khi đồng ý, Thủ kho ghi số lượng thực nhập và lập phiếu; chỉ lúc này lô mới có trạng thái In Stock. QC không đạt không được yêu cầu nhập thành phẩm. Không phụ thuộc khai báo tiêu hao hoặc Manager duyệt.

Giữ đúng nhánh sơ đồ đã chốt: có chênh lệch → thông báo Manager; không chênh lệch → Staff xác nhận phiếu → thông báo Manager. Ghi chú đối chiếu bắt buộc khi chênh lệch. Tồn kho được ghi nhận một lần, Manager chỉ theo dõi.

Ảnh kết quả QC bắt buộc với kết quả mới, hỗ trợ JPG/PNG/WebP tối đa 5 MB, lưu trong database. Endpoint xem ảnh kiểm tra quyền theo role; Staff chỉ xem ảnh yêu cầu của mình. Kết quả QC cũ chưa có ảnh vẫn được giữ để tra cứu.

Manager xem hai mục **Nguyên liệu tiêu thụ** và **Yêu cầu nhập kho thành phẩm** trong nhóm **Báo cáo cuối ca** tại `/ShiftReports/Index`. Các liên kết báo cáo cũ của Staff và Owner chuyển tới mục tiêu hao.

### Dự kiến nguyên liệu và phiếu chờ Thủ kho

Trong **Phân công lao động**, Manager lập từng ca hoặc lập cả ngày trong một phiếu. Ca làm việc có nguyên liệu tiêu thụ dự kiến, thành phẩm dự kiến và danh sách công nhân theo công đoạn. Manager có thể chỉnh sửa hoặc đánh dấu nghỉ cho ca của ngày hiện tại. Ca đã có báo cáo hoặc yêu cầu nghiệp vụ được bảo vệ khỏi việc chuyển sang nghỉ. Thủ kho nhận thông báo khi lịch được cập nhật.

Staff gửi hoặc sửa/gửi lại phiếu tiêu hao sẽ thông báo tới các tài khoản Thủ kho đang hoạt động. Menu **Kho nguyên liệu** của Thủ kho có hai mục riêng: **Phiếu nhập kho nguyên liệu** và **Phiếu tiêu hao nguyên liệu**. Mỗi mục đếm số phiếu đang cần Thủ kho xử lý; không đếm số thông báo chưa đọc. Phiếu nhập tính các trạng thái Pending, WaitWarehouse, ImportRejected (và dữ liệu cũ chưa có trạng thái); tiêu hao tính Pending. Đã xử lý thì số giảm, gửi lại thì số tăng. Các số hiện trên mọi trang của Thủ kho, tự cập nhật sau tối đa 30 giây khi trang đang mở, đồng thời cập nhật khi quay lại cửa sổ. Số 0 được ẩn. Mục cha hiển thị tổng hai số. Mục tiêu hao có đối chiếu dự kiến và mặc định hiển thị phiếu chờ trên tất cả các ngày. Trang Kế hoạch trong ngày chỉ hiển thị các phân công đúng ngày chọn, không phải nơi nhận phiếu Staff.

Thủ kho đối chiếu phiếu tiêu hao với dự kiến của đúng ca và ngày. Phiếu của các ca cũ dùng số dự kiến theo ngày. Khi duyệt, hệ thống trừ khối lượng thực dùng theo tồn thực tế. Khi từ chối, Thủ kho ghi lý do để Staff sửa và gửi lại. Phiếu nhập thành phẩm sau QC được xử lý ở mục Nhập kho thành phẩm.

Phiếu nhập kho nguyên liệu và tồn kho giữ tại `/Inventory/Index?tab=raw`; phiếu tiêu hao mở riêng tại `/Warehouse/Consumption`. Giữ nguyên form lập PNK và luồng IR → Thủ kho lập phiếu → Manager duyệt nhập kho. Liên kết thông báo tiêu hao cũ có `UsageStatusFilter` được chuyển tới đúng trang tiêu hao. Staff tiếp tục dùng màn hình tiêu hao hiện có.

### Số đếm trên toàn bộ menu Thủ kho

- **Kế hoạch sản xuất trong ngày**: số thông báo kế hoạch chưa đọc của chính tài khoản Thủ kho. Mở mục kế hoạch thì đánh dấu đã đọc nhóm này.
- **Nhập kho thành phẩm**: số yêu cầu đang Pending, giảm khi Thủ kho nhận hoặc từ chối; Staff gửi lại thì tăng.
- **Kho nguyên liệu**: giữ nguyên tổng hai loại phiếu nhập nguyên liệu và tiêu hao đang chờ xử lý.
- **Kho thành phẩm**: số thông báo cập nhật kho chưa đọc của chính tài khoản. Khi Thủ kho nhập thành phẩm, hệ thống gửi thông báo cho các tài khoản Thủ kho đang hoạt động; mở Kho thành phẩm thì đánh dấu đã đọc nhóm này.

Mở mục khác không đánh dấu hai nhóm thông báo chỉ xem này là đã đọc; đọc của một tài khoản không ảnh hưởng tài khoản khác. Các số không có nền/khung, ẩn khi bằng 0 và tự cập nhật khi quay lại cửa sổ hoặc mỗi 30 giây. Không đổi biểu mẫu hay luồng phê duyệt hiện có, không cần thêm migration.

### Tổng quan vận hành của Thủ kho

Đăng nhập Thủ kho mở trang Tổng quan vận hành. Số tồn nguyên liệu cộng các dòng có đơn vị kg; số tồn thành phẩm lấy các lô In Stock, số lô chỉ tính lô còn khối lượng. Lịch sử nhập/xuất lấy phiếu nhập nguyên liệu Approved (thời điểm duyệt nếu có), xuất nguyên liệu đã ghi nhận, phiếu nhập thành phẩm đã nhận thực tế và chi tiết đơn hàng có giao hàng In Transit/Delivered. Một chi tiết đơn xuất chỉ xuất hiện một lần, theo lần giao đã ghi nhận đầu tiên, kể cả đơn có nhiều bản ghi giao hàng; không giả định chia khối lượng theo xe vì schema chưa phân bổ chi tiết theo xe. Không lấy phiếu mua/chứng từ đang chờ hoặc giao hàng Prepared làm lịch sử xuất kho. Dữ liệu tồn và lịch sử được đọc từ các bảng gốc, không tạo dữ liệu mẫu hoặc trừ/cộng lại tồn kho khi xem.

Lịch sử có lọc khoảng ngày, nhập/xuất, nhóm nguyên liệu/thành phẩm và phân trang 10 dòng, mới nhất trước. Trang Kế hoạch sản xuất của Thủ kho chỉ hiển thị bảng phân công, lọc ngày và phân trang; không có nút duyệt nghiệp vụ ở trang này. Không thay đổi các luồng tạo, duyệt, từ chối hay nhập/xuất hiện có.

### Tra cứu ca sản xuất của Thủ kho

Mở mục Kế hoạch sản xuất tự hiển thị kế hoạch hôm nay, còn ô tìm ngày để trống. Thủ kho chọn ngày khác rồi bấm Tìm kiếm; kết quả chỉ lấy phân công đúng ngày đó. Mỗi trang hiển thị trọn một ca, gồm tất cả công việc và nhân viên của ca, không chia ca theo giới hạn 10 dòng. Mỗi bảng đặt ca và ngày ở đầu bên trái, các cột chỉ gồm STT, công việc/máy móc, nhân viên, người phân công; STT bắt đầu từ 1 trong mỗi ca. Các nút chọn ca xếp thành một hàng ngang phía trên bảng, ca đang xem có màu xanh; bấm trực tiếp để chuyển ca của ngày đang xem, tìm ngày khác quay về ca đầu tiên. Ngày không có dữ liệu báo trống; ngày thiếu/sai không tự lấy dữ liệu hôm nay. Trang này chỉ xem, không thay đổi phân công hay tồn kho. Các danh sách khác giữ phân trang 10 dòng.
