-- ====================================================================
-- Script sửa lỗi phông chữ Tiếng Việt hoàn toàn (UTF-8 with BOM)
-- Cập nhật toàn bộ bảng: User, Role, Customer, Supplier, 
-- EndOfShiftReport, MaintenanceRequest, PurchaseInvoice, BrokenEquipmentReport
-- ====================================================================
USE ProductionManagementDB;
GO

SET NOCOUNT ON;
BEGIN TRANSACTION;

-- 1. Cột bảng User & Role
ALTER TABLE [User] ALTER COLUMN full_name NVARCHAR(100) NOT NULL;
ALTER TABLE [Role] ALTER COLUMN description NVARCHAR(MAX);

UPDATE [User] SET full_name = N'Ngô Quang Khải (Quản trị viên Hệ thống)' WHERE username IN ('admin', 'admin_sys');
UPDATE [User] SET full_name = N'Nguyễn Văn Thành (Giám đốc)' WHERE username = 'owner_batt';
UPDATE [User] SET full_name = N'Trần Quốc Bảo (Trưởng phòng Vận hành)' WHERE username = 'manager_sx';
UPDATE [User] SET full_name = N'Hoàng Văn Hùng (Tổ trưởng Sản xuất)' WHERE username = 'congnhan_hung';
UPDATE [User] SET full_name = N'Vũ Tiến Dũng (Kỹ sư Cơ điện)' WHERE username = 'baotri_dung';
UPDATE [User] SET full_name = N'Đỗ Thị Ngọc Lan (Trưởng nhóm KCS)' WHERE username = 'kcs_lan';
UPDATE [User] SET full_name = N'Lê Văn Quang (Thu mua)' WHERE username = 'muahang_quang';
UPDATE [User] SET full_name = N'Phạm Thị Phương Thảo (Thủ kho)' WHERE username = 'thukho_thao';
UPDATE [User] SET full_name = N'Đặng Nhật Minh (Công nhân)' WHERE username = 'congnhan_minh';
UPDATE [User] SET full_name = N'Nguyễn Mạnh Cường' WHERE username = 'quanly';

UPDATE [Role] SET description = N'Quản trị viên hệ thống' WHERE role_name = 'Admin';
UPDATE [Role] SET description = N'Chủ doanh nghiệp / Giám đốc' WHERE role_name = 'Owner';
UPDATE [Role] SET description = N'Quản lý sản xuất & Vận hành' WHERE role_name = 'Manager';
UPDATE [Role] SET description = N'Nhân viên sản xuất' WHERE role_name = 'Staff';
UPDATE [Role] SET description = N'Nhân viên bảo trì cơ điện' WHERE role_name = 'Maintenance Staff';
UPDATE [Role] SET description = N'Nhân viên kiểm tra chất lượng (QC)' WHERE role_name = 'QC Staff';
UPDATE [Role] SET description = N'Nhân viên thu mua nguyên liệu' WHERE role_name = 'Purchasing Staff';
UPDATE [Role] SET description = N'Nhân viên thủ kho' WHERE role_name = 'Warehouse Staff';

-- 2. Cột bảng Supplier & Customer
ALTER TABLE [Supplier] ALTER COLUMN supplier_name NVARCHAR(100) NOT NULL;
ALTER TABLE [Supplier] ALTER COLUMN contact_person NVARCHAR(100);
ALTER TABLE [Supplier] ALTER COLUMN address NVARCHAR(MAX);
ALTER TABLE [Supplier] ALTER COLUMN notes NVARCHAR(MAX);

ALTER TABLE [Customer] ALTER COLUMN customer_name NVARCHAR(100) NOT NULL;
ALTER TABLE [Customer] ALTER COLUMN contact_person NVARCHAR(100);
ALTER TABLE [Customer] ALTER COLUMN address NVARCHAR(MAX);
ALTER TABLE [Customer] ALTER COLUMN payment_terms NVARCHAR(MAX);

DELETE FROM Supplier;
DELETE FROM Customer;

INSERT INTO Supplier (supplier_name, contact_person, phone, email, address, notes, status, created_by)
VALUES 
(N'Công ty TNHH Lâm Nghiệp Hàm Thuận', N'Nguyễn Đức Thắng', '0252.386.1111', 'thang.nd@hamthuanwood.vn', N'KCN Hàm Kiệm, Huyện Hàm Thuận Nam, Bình Thuận', N'Đối tác chiến lược cung cấp nguyên vật liệu dăm & mùn cưa', 'Active', 2),
(N'Xưởng Chế biến Gỗ Minh Phú', N'Lê Thị Mỹ', '0252.399.2222', 'my.lt@minhphuwood.vn', N'Thị trấn Ma Lâm, Hàm Thuận Bắc, Bình Thuận', N'Cung cấp dăm gỗ cao su và thông xẻ', 'Active', 2),
(N'Công ty Cổ phần Nông Lâm sản Bình Thuận', N'Phan Trọng Hùng', '0252.375.3333', 'hung.pt@binhthuanagri.vn', N'Xã Hàm Cường, Hàm Thuận Nam, Bình Thuận', N'Cung ứng vỏ trấu và phụ phẩm nông lâm nghiệp', 'Active', 2);

INSERT INTO Customer (customer_name, contact_person, phone, email, address, payment_terms, status, created_by)
VALUES 
(N'Sumitomo Forestry Co., Ltd (Japan)', N'Kenji Sato / Trần Anh Tuấn', '+81.3.3286.7000', 'sales@sumitomoforestry.co.jp', N'Chiyoda-ku, Tokyo, Japan / Chi nhánh TP.HCM', N'LC At Sight, kiểm hàng ENplus-A1 tại cảng Cái Mép', 'Active', 2),
(N'Doosan Heavy Industries & Construction (Korea)', N'Park Ji Sung / Vũ Linh', '+82.55.278.6114', 'biomass@doosan.com', N'Changwon, Gyeongnam, Korea', N'T/T 30% trước, 70% sau khi nhận Bill of Lading', 'Active', 2),
(N'Công ty Cổ phần Nhiệt điện Thăng Long', N'Đỗ Đình Toàn', '024.3984.5566', 'toan.dd@thanglongpower.vn', N'Khu kinh tế Đình Vũ, Hải Phòng', N'Hợp đồng nguyên tắc, thanh toán theo kỳ đối soát 15 ngày', 'Active', 2);

-- 3. Bảng EndOfShiftReport (Daily Reports)
ALTER TABLE [EndOfShiftReport] ALTER COLUMN shift NVARCHAR(50);
ALTER TABLE [EndOfShiftReport] ALTER COLUMN note NVARCHAR(MAX);
ALTER TABLE [EndOfShiftReport] ALTER COLUMN incident_description NVARCHAR(MAX);
ALTER TABLE [EndOfShiftReport] ALTER COLUMN rejection_reason NVARCHAR(MAX);

UPDATE [EndOfShiftReport] SET 
    shift = N'Ca 1 (06:00 - 14:00)',
    note = N'Máy ép chạy êm, viên nén bóng đều đạt chuẩn xuất khẩu, đóng đủ 20 bao Jumbo 1 tấn',
    incident_description = NULL
WHERE report_id = 0 OR report_id = 1;

UPDATE [EndOfShiftReport] SET 
    shift = N'Ca 2 (14:00 - 22:00)',
    note = N'Vận hành ổn định, độ ẩm thành phẩm đạt chuẩn 7.8%',
    incident_description = NULL
WHERE report_id = 2;

UPDATE [EndOfShiftReport] SET 
    shift = N'Ca 2 (14:00 - 22:00)',
    note = N'Tỷ lệ thu hồi giảm nhẹ do dừng máy xử lý kẹt nguyên liệu ẩm',
    incident_description = N'Nghẹt phễu cấp liệu trục vít số 2 gây dừng máy 45 phút'
WHERE report_id = 3;

-- 4. Bảng MaintenanceRequest (Repair Requests)
ALTER TABLE [MaintenanceRequest] ALTER COLUMN description NVARCHAR(MAX);
ALTER TABLE [MaintenanceRequest] ALTER COLUMN completion_note NVARCHAR(MAX);
ALTER TABLE [MaintenanceRequest] ALTER COLUMN rejection_reason NVARCHAR(MAX);

UPDATE [MaintenanceRequest] SET 
    description = N'Cần kiểm tra thay thế vòng bi SKF và bôi trơn lại mỡ chịu nhiệt cao trục máy ép số 1',
    completion_note = NULL
WHERE request_id = 0;

UPDATE [MaintenanceRequest] SET 
    description = N'Dán vá nguội băng tải cao su và kiểm tra lại bộ nam châm hút sắt phễu nạp liệu',
    completion_note = NULL
WHERE request_id = 1;

UPDATE [MaintenanceRequest] SET 
    description = N'Đã hiệu chuẩn lại cảm biến loadcell bằng quả cân chuẩn 500kg bàn cân số 1',
    completion_note = N'Đã kiểm chuẩn xác sai số dưới 0.1%, bàn giao lại cho tổ sản xuất vận hành'
WHERE request_id = 2;

UPDATE [MaintenanceRequest] SET 
    description = N'Chờ Giám đốc phân công kỹ sư cơ điện kiểm tra khớp nối trục động cơ quạt hút sấy mùn cưa',
    completion_note = NULL
WHERE request_id = 3;

-- 5. Bảng BrokenEquipmentReport
ALTER TABLE [BrokenEquipmentReport] ALTER COLUMN equipment_name NVARCHAR(100) NOT NULL;
ALTER TABLE [BrokenEquipmentReport] ALTER COLUMN description NVARCHAR(MAX);

UPDATE [BrokenEquipmentReport] SET 
    equipment_name = N'Máy ép viên nén số 01 (Ring Die Pellet Mill)',
    description = N'Trục ép phát ra tiếng rít kim loại khi chạy tải trên 80%, nhiệt độ vòng bi tăng lên 78°C'
WHERE report_id = 0;

UPDATE [BrokenEquipmentReport] SET 
    equipment_name = N'Băng tải cấp mùn cưa BC-02',
    description = N'Băng tải bị rách mép dọc 25cm tại đoạn nối cơ khí, có nguy cơ kẹt con lăn hồi'
WHERE report_id = 1;

UPDATE [BrokenEquipmentReport] SET 
    equipment_name = N'Bàn cân xe điện tử 80 Tấn',
    description = N'Cảm biến góc loadcell số 3 dao động số liệu +/- 40kg khi xe tải nặng đi vào bàn cân'
WHERE report_id = 2;

UPDATE [BrokenEquipmentReport] SET 
    equipment_name = N'Quạt hút sấy mùn cưa F-102',
    description = N'Rung giật cơ khí cấp độ 3, phát hiện ốc chân đế động cơ bị lỏng ren'
WHERE report_id = 3;

-- 6. Bảng PurchaseInvoice (Purchase Orders & Reports)
ALTER TABLE [PurchaseInvoice] ALTER COLUMN raw_material_type NVARCHAR(100);
ALTER TABLE [PurchaseInvoice] ALTER COLUMN note NVARCHAR(MAX);
ALTER TABLE [PurchaseInvoice] ALTER COLUMN rejection_reason NVARCHAR(MAX);

UPDATE [PurchaseInvoice] SET 
    raw_material_type = N'Mùn cưa cao su & thông',
    note = N'Đã cân tại trạm cân Hàm Kiệm, kiểm tra độ ẩm sơ bộ đạt 12.5%, đủ tiêu chuẩn ép viên'
WHERE invoice_id = 0;

UPDATE [PurchaseInvoice] SET 
    raw_material_type = N'Dăm bào gỗ tạp khô',
    note = N'Dăm gỗ khô chất lượng tốt, Owner đã duyệt chuyển giao kho nhập'
WHERE invoice_id = 1;

UPDATE [PurchaseInvoice] SET 
    raw_material_type = N'Mùn cưa rừng trồng',
    note = N'Phiếu cân trạm Hàm Thuận Nam, hàng về đủ 2 xe tải lớn, chờ Giám đốc duyệt tiếp nhận'
WHERE invoice_id = 2;

UPDATE [PurchaseInvoice] SET 
    raw_material_type = N'Vỏ trấu & phụ phẩm ván',
    note = N'Yêu cầu bổ sung hóa đơn VAT và phiếu cân gốc có chữ ký tài xế'
WHERE invoice_id = 3;

UPDATE [PurchaseInvoice] SET 
    raw_material_type = N'Dăm bào thông xẻ',
    note = N'Đã phân công xe đến xưởng Minh Phú nhận hàng sáng 26/10'
WHERE invoice_id = 4;

-- 7. Bảng InventoryItem
ALTER TABLE [InventoryItem] ALTER COLUMN item_name NVARCHAR(100) NOT NULL;
ALTER TABLE [InventoryItem] ALTER COLUMN unit NVARCHAR(20);

UPDATE [InventoryItem] SET 
    item_name = N'Mùn cưa gỗ cao su nguyên liệu',
    unit = N'Tấn'
WHERE item_id = 1;

UPDATE [InventoryItem] SET 
    item_name = N'Dăm bào gỗ thông khô',
    unit = N'Tấn'
WHERE item_id = 2;

UPDATE [InventoryItem] SET 
    item_name = N'Viên nén gỗ xuất khẩu ENplus-A1',
    unit = N'Tấn'
WHERE item_id = 3;

COMMIT TRANSACTION;
GO
PRINT 'DONE_UNICODE_FIX';
