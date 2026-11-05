# BATT_SEP490_G143_Fall2026

## Luồng sản xuất, QC và nhập kho

Với database đã có dữ liệu, chạy `Add_Production_QC_Warehouse_Workflow.sql`, sau đó `Add_ShiftMaterialUsage.sql` trên SQL Server của dự án rồi khởi động lại ứng dụng. Các script chạy lại được và không xóa dữ liệu cũ. Không chạy lại script tạo database nền để cập nhật luồng này. Bảng yêu cầu cấp nguyên liệu cũ chỉ được giữ để bảo toàn lịch sử; chức năng yêu cầu/cấp nguyên liệu đã được bỏ khỏi giao diện và các POST handler.

1. Manager lập kế hoạch, phân công. Staff và Thủ kho xem lịch sản xuất.
2. Không có yêu cầu cấp nguyên liệu trước sản xuất. Staff khai báo từng nguyên liệu và lượng thực tế đã dùng trong báo cáo cuối ca. Chỉ khi Manager duyệt, hệ thống tạo phiếu ghi nhận tiêu hao và trừ tồn trong cùng giao dịch. Từ chối, gửi lại hoặc sửa báo cáo chưa duyệt không làm thay đổi tồn kho. Duyệt lặp bị chặn.
3. Staff gửi yêu cầu kiểm tra lô từ mục Báo cáo cuối ca → Yêu cầu và kết quả QC.
4. QC tiếp nhận, kiểm tra và trả kết quả đạt/không đạt kèm kết luận. Manager và Owner được xem kết quả.
5. Khi đã có kết quả QC, Staff lập báo cáo cuối ca cho đúng lô, ngày, ca, sản lượng đã kiểm tra. Một yêu cầu QC chỉ gắn một báo cáo. Manager duyệt hoặc từ chối; Owner chỉ xem.
6. Staff chỉ yêu cầu nhập thành phẩm khi QC đạt và báo cáo đã được duyệt. Lô không đạt vẫn được ghi nhận trong báo cáo, nhưng không được yêu cầu nhập kho.
7. Thủ kho đối chiếu và lập phiếu nhập hoặc trả lý do từ chối. Staff nhập nội dung điều chỉnh rồi gửi lại yêu cầu bị từ chối; không được tự đổi lô hoặc sản lượng đã được QC kiểm tra và Manager duyệt. Lô chỉ được tính tồn sau khi Thủ kho xác nhận nhập.
8. Giữ nhánh đã chốt theo sơ đồ: có chênh lệch → Manager xem thông tin; không chênh lệch → Staff xác nhận phiếu → Manager xem thông tin.

Phiếu QC cũ vẫn có mục tra cứu riêng. Báo cáo cũ chưa có liên kết QC cần được Staff bổ sung một kết quả QC hợp lệ khi chỉnh sửa trước khi Manager phê duyệt. Báo cáo cũ đã duyệt được giữ nguyên.
