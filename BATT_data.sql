USE ProductionManagementDB;
GO
-- 1. Thêm dữ liệu bảng Role (Phân quyền)
INSERT INTO [Role] (role_name, description) VALUES
(N'Admin System', N'Quản trị viên hệ thống'),
(N'Owner', N'Chủ doanh nghiệp'),
(N'Manager', N'Quản lý sản xuất và kinh doanh'),
(N'Warehouse Staff', N'Thủ kho (quản lý nguyên liệu và thành phẩm)'),
(N'QC Staff', N'Nhân viên kiểm tra chất lượng'),
(N'Maintenance Staff', N'Nhân viên bảo trì thiết bị'),
(N'Staff', N'Nhân viên sản xuất trực tiếp'),
(N'Purchasing Staff', N'Nhân viên thu mua');
GO

-- 2. Thêm dữ liệu bảng User (Nhân sự)
INSERT INTO [User] (username, password, full_name, status, phone, email, role_id) VALUES
('admin01', 'hashed_password', N'Nguyễn Văn Admin', 'Active', '0901111111', 'admin@factory.com', 1),
('owner01', 'hashed_password', N'Trần Văn Chủ', 'Active', '0902222222', 'owner@factory.com', 2),
('manager01', 'hashed_password', N'Lê Thị Quản Lý', 'Active', '0903333333', 'manager@factory.com', 3),
('keeper01', 'hashed_password', N'Phạm Văn Kho', 'Active', '0904444444', 'kho@factory.com', 4),
('qc01', 'hashed_password', N'Hoàng Thị QC', 'Active', '0905555555', 'qc@factory.com', 5),
('maint01', 'hashed_password', N'Vũ Văn Thợ', 'Active', '0906666666', 'baotri@factory.com', 6),
('staff01', 'hashed_password', N'Ngô Văn Thợ Cả', 'Active', '0907777777', 'tho1@factory.com', 7),
('purchasing01', 'hashed_password', N'Thắng', 'Active', '0865973208', 'thummua@factory.com', 8);
GO

-- 3. Thêm dữ liệu bảng Customer (Khách hàng - do Owner/Manager tạo)
INSERT INTO Customer (customer_name, contact_person, phone, email, address, status, created_by) VALUES
(N'Công ty TNHH Thương mại ABC', N'Anh Nam', '09811122233', 'contact@abc.com', N'Hà Nội', 'Active', 2),
(N'Cổ phần Đầu tư XYZ', N'Chị Hương', '09844455566', 'info@xyz.vn', N'TP. Hồ Chí Minh', 'Active', 3);
GO

-- 4. Thêm dữ liệu bảng Supplier (Nhà cung cấp - do Owner tạo)
INSERT INTO Supplier (supplier_name, contact_person, phone, email, address, status, created_by) VALUES
(N'Công ty Cung ứng Nguyên liệu Miền Bắc', N'Ông Bình', '0912345678', 'sales@supplierbac.com', N'Hải Phòng', 'Active', 2);
GO

-- 5. Thêm dữ liệu bảng InventoryItem (Kho thành phẩm chính để check tồn khi lập đơn)
INSERT INTO InventoryItem (item_code, item_name, unit, quantity, min_quantity, status) VALUES
('FG-001', N'Sản phẩm nhựa định hình loại A', N'Cái', 500, 100, 'Available'),
('FG-002', N'Sản phẩm nhựa cao cấp loại B', N'Cái', 200, 50, 'Available');
GO

-- 6. Thêm dữ liệu bảng RawMaterial (Kho nguyên liệu duy nhất)
INSERT INTO RawMaterial (material_name, unit, current_quantity) VALUES
(N'Hạt nhựa nguyên sinh PP', 'kg', 5000.00);
GO

-- 7. Thêm dữ liệu bảng RawMaterialImport (Lịch sử nhập nguyên liệu)
INSERT INTO RawMaterialImport (material_id, supplier_id, quantity, unit_price, imported_by, note) VALUES
(1, 1, 2000.00, 25000.00, 4, N'Nhập lô hạt nhựa nguyên sinh đầu tháng');
GO

-- 8. Thêm dữ liệu bảng RawMaterialExport (Lịch sử xuất nguyên liệu sản xuất)
INSERT INTO RawMaterialExport (material_id, quantity, exported_by, note) VALUES
(1, 500.00, 4, N'Xuất nguyên liệu phục vụ dây chuyền sản xuất ca sáng');
GO

-- 9. Thêm dữ liệu bảng FinishedGoodBatch (Các bao/lô thành phẩm đánh số theo ngày)
INSERT INTO FinishedGoodBatch (bag_number, production_date, quantity, status, created_by) VALUES
('BAG-2026-06-07-01', '2026-06-07', 150.00, 'In Stock', 7),
('BAG-2026-06-07-02', '2026-06-07', 150.00, 'In Stock', 7);
GO

-- 10. Thêm dữ liệu bảng QCInspection (Kiểm tra chất lượng bao thành phẩm)
INSERT INTO QCInspection (inspection_code, batch_id, request_date, result, note, status, requested_by, inspected_by, inspect_date) VALUES
('QC-2026-001', 1, '2026-06-07 16:00:00', 'Pass', N'Đạt tiêu chuẩn chất lượng bề mặt và độ bền', 'Completed', 3, 5, '2026-06-07 16:30:00');
GO

-- 11. Thêm dữ liệu bảng PersonnelAssignment (Phân công nhân sự)
INSERT INTO PersonnelAssignment (staff_id, assigned_by, assignment_date, shift, task_description, status) VALUES
(7, 3, '2026-06-07', N'Ca Sáng', N'Vận hành máy đùn nhựa số 1', 'Assigned');
GO

-- 12. Thêm dữ liệu bảng EndOfShiftReport (Báo cáo cuối ca / sản lượng)
INSERT INTO EndOfShiftReport (assignment_id, staff_id, shift_date, production_quantity, note, status) VALUES
(1, 7, '2026-06-07', 300, N'Hoàn thành định mức sản lượng ca sáng suôn sẻ', 'Submitted');
GO

-- 13. Thêm dữ liệu bảng Order (Đơn hàng)
INSERT INTO [Order] (order_code, customer_id, inventory_id, quantity, unit_price, total_amount, status, approved_by, created_by, order_date) VALUES
('ORD-2026-001', 1, 1, 100, 50000.00, 5000000.00, 'Approved', 2, 3, '2026-06-07 10:00:00');
GO

-- 14. Thêm dữ liệu bảng OrderDetail (Chi tiết đơn hàng gắn với bao thành phẩm cụ thể)
INSERT INTO OrderDetail (order_id, batch_id, quantity) VALUES
(1, 1, 100.00);
GO

-- 15. Thêm dữ liệu bảng Delivery (Giao hàng)
INSERT INTO Delivery (order_id, license_plate, driver_name, pickup_date, delivery_status, confirmed_by) VALUES
(1, '29C-123.45', N'Nguyễn Văn Tài Xế', '2026-06-07 14:00:00', 'Prepared', 4);
GO

-- 16. Thêm dữ liệu bảng Machine (Máy móc)
IF NOT EXISTS (SELECT * FROM Machine WHERE machine_code = 'M-01')
BEGIN
    INSERT INTO Machine (machine_code, machine_name, process_name, brand, created_by) VALUES
    ('M-01', N'Máy băm nghiền dăm bào', N'Sàng lọc tạp chất & băm dăm bào', 'BATT Standard', 2),
    ('M-02', N'Máy sấy thùng quay', N'Sấy mùn cưa hạ ẩm', 'BATT Standard', 2),
    ('M-03A', N'Cụm máy ép viên A', N'Ép viên nén sinh khối', 'BATT Standard', 2),
    ('M-03B', N'Cụm máy ép viên B', N'Ép viên nén sinh khối', 'BATT Standard', 2),
    ('M-04', N'Cân đóng bao tự động', N'Đóng bao Jumbo & lưu kho', 'BATT Standard', 2);
END
GO

-- 17. Thêm dữ liệu bảng BrokenEquipmentReport (Báo cáo thiết bị hỏng)
INSERT INTO BrokenEquipmentReport (machine_id, description, reported_by, report_date, status, severity) VALUES
(1, N'Phát ra tiếng kêu lạ ở trục quay và nhiệt độ tăng cao bất thường', 7, '2026-06-07 11:30:00', 'Reported', 'Medium');
GO

-- 18. Thêm dữ liệu bảng MaintenanceRequest (Yêu cầu bảo trì)
INSERT INTO MaintenanceRequest (broken_report_id, requested_by, assigned_to, request_date, status, priority, description) VALUES
(1, 3, 6, '2026-06-07 11:45:00', 'Assigned', 'High', N'Kiểm tra và bôi trơn/thay thế ổ bi trục quay máy đùn số 1');
GO

-- 19. Thêm dữ liệu bảng MaterialPurchaseRequest (Yêu cầu mua vật tư sửa chữa)
INSERT INTO MaterialPurchaseRequest (maintenance_request_id, item_name, quantity, requested_by, status, request_date, approved_by) VALUES
(1, N'Ổ bi trục quay chịu nhiệt cỡ lớn', 2, 6, 'Approved', '2026-06-07 12:00:00', 2);
GO

-- 20. Thêm dữ liệu bảng PurchaseInvoice (Hóa đơn mua hàng từ nhà cung cấp)
INSERT INTO PurchaseInvoice (invoice_code, supplier_id, purchase_date, total_amount, status, created_by, approved_by, note) VALUES
('INV-2026-001', 1, '2026-06-06', 50000000.00, 'Approved', 4, 2, N'Hóa đơn mua nguyên liệu hạt nhựa tháng 6');
GO

-- 21. Thêm dữ liệu bảng PurchaseInvoiceDetail (Chi tiết hóa đơn mua hàng liên kết RawMaterial)
INSERT INTO PurchaseInvoiceDetail (invoice_id, material_id, quantity, unit_price) VALUES
(1, 1, 2000.00, 25000.00);
GO