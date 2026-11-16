# BATT_SEP490_G143_Fall2026

## Cập nhật database sau khi pull luồng bảo trì

Với database đang có dữ liệu, chạy `Add_MaintenanceRequest_Workflow.sql` trước, rồi chạy `Add_MaintenanceMaterialFlow.sql` trên database ứng dụng đang sử dụng. Hai script bổ sung các cột bảo trì, nhật ký và thông báo còn thiếu, có thể chạy lại. Không cần chạy lại file tạo database nền. Nếu gặp `Invalid column name` với `due_date`, `location`, `machine_stopped`, `rejection_reason` hoặc `title`, database chưa được cập nhật phần cấu trúc bảo trì.

## Báo cáo cuối ca: hai luồng độc lập

Staff nhập tổng số bao thành phẩm của ca khi gửi QC. Hệ thống quy đổi 800 kg mỗi bao và dùng cột khối lượng hiện có cho tồn kho, báo cáo và giao dịch. Mỗi ca ghi nhận một lô tổng. Ví dụ Ca 1 ngày 12/11/2026 sản xuất 18 bao dùng mã `LOT-261112-C1-1-18`. Nếu ca có phát sinh sự cố, Staff ghi lý do và hệ thống cấp `LOT-261112-C1-2-18` cho cùng tổng 18 bao. QC và Thủ kho dùng mã đã cấp, kể cả khi kiểm tra lại. Các mã đã lưu trước đây vẫn dùng để tra cứu lịch sử.

Với database đang có dữ liệu, chạy lần lượt các script còn thiếu theo thứ tự `Add_Production_QC_Warehouse_Workflow.sql`, `Add_ShiftMaterialUsage.sql`, `Add_MaterialUsageReview.sql`, `Add_QC_ResultImage.sql`, `Add_QC_ResultDetails.sql` và `Add_DailyMaterialPlan.sql`. Sau đó chạy bản mới của `Add_ShiftProductionPlan.sql` để bổ sung trạng thái ca nghỉ, kể cả khi đã chạy bản trước. Script giữ các phân công và phiếu QC đã lưu. Khởi động lại ứng dụng sau khi cập nhật.

Menu Staff “Báo cáo cuối ca làm việc” có hai mục:

1. **Nguyên liệu tiêu thụ trong ca làm việc**: Staff được giao **Máy băm** trong đúng ngày và ca mới nhập khối lượng thực dùng theo kg để gửi Thủ kho. Mỗi ca có một phiếu tiêu hao. Staff có thể chọn ngày trước để nhập bù cho ca chưa có phiếu, hoặc sửa phiếu của mình đang chờ Thủ kho duyệt. Thủ kho từ chối phải nhập lý do để người gửi có phân công Máy băm cùng ca sửa và gửi lại. Khi Thủ kho duyệt, hệ thống tạo phiếu xuất và trừ tồn trong cùng giao dịch. Các dòng tồn kho cùng tên nguyên liệu, đơn vị kg được tính tổng và trừ theo mã nguyên liệu tăng dần. Phiếu đã duyệt chỉ xem.
2. **Yêu cầu nhập kho thành phẩm**: Staff gửi yêu cầu nhập kho sau khi QC trả kết quả đạt và ảnh kiểm tra. Khi Thủ kho đồng ý, hệ thống ghi nhận khối lượng thực nhập, cập nhật tồn kho ngay và thông báo cho Manager. Nếu từ chối, Thủ kho nhập lý do để hệ thống thông báo cho Staff. Staff có thể sửa và gửi lại. Manager xem lý do từ chối trong báo cáo cuối ca. Ghi chú đối chiếu bắt buộc khi khối lượng thực nhập chênh lệch.

Ảnh kết quả QC bắt buộc với kết quả mới, hỗ trợ JPG/PNG/WebP tối đa 5 MB, lưu trong database. Endpoint xem ảnh kiểm tra quyền theo role; Staff chỉ xem ảnh yêu cầu của mình. Kết quả QC cũ chưa có ảnh vẫn được giữ để tra cứu.

Manager xem hai mục **Nguyên liệu tiêu thụ** và **Yêu cầu nhập kho thành phẩm** trong nhóm **Báo cáo cuối ca** tại `/ShiftReports/Index`. Các liên kết báo cáo cũ của Staff và Owner chuyển tới mục tiêu hao.

### Dự kiến nguyên liệu và phiếu chờ Thủ kho

Trong **Phân công công việc**, Manager lập từng ca hoặc nhiều ca trong một phiếu từ hôm nay trở đi. Ca làm việc có nguyên liệu tiêu thụ dự kiến, thành phẩm dự kiến và danh sách công nhân theo công đoạn. Với ngày hiện tại, Manager có thể chỉnh sửa phân công hoặc chọn **Nghỉ** trong dropdown của từng ca. Ca đã có báo cáo hoặc yêu cầu nghiệp vụ được bảo vệ khỏi việc chuyển sang nghỉ. Thủ kho nhận thông báo khi lịch được cập nhật.

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
