-- ====================================================================
-- Script sửa lỗi phông chữ Tiếng Việt (Unicode Mojibake / Collation)
-- Chuyển đổi các cột VARCHAR sang NVARCHAR và cập nhật dữ liệu có dấu
-- ====================================================================
USE ProductionManagementDB;
GO

BEGIN TRANSACTION;

-- 1. Chuyển đổi các cột văn bản tiếng Việt sang NVARCHAR
ALTER TABLE [User] ALTER COLUMN full_name NVARCHAR(100) NOT NULL;

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Role')
    ALTER TABLE [Role] ALTER COLUMN description NVARCHAR(MAX);

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Customer')
BEGIN
    ALTER TABLE [Customer] ALTER COLUMN customer_name NVARCHAR(100) NOT NULL;
    ALTER TABLE [Customer] ALTER COLUMN contact_person NVARCHAR(100);
    ALTER TABLE [Customer] ALTER COLUMN address NVARCHAR(MAX);
END

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Supplier')
BEGIN
    ALTER TABLE [Supplier] ALTER COLUMN supplier_name NVARCHAR(100) NOT NULL;
    ALTER TABLE [Supplier] ALTER COLUMN contact_person NVARCHAR(100);
    ALTER TABLE [Supplier] ALTER COLUMN address NVARCHAR(MAX);
END

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'InventoryItem')
BEGIN
    ALTER TABLE [InventoryItem] ALTER COLUMN item_name NVARCHAR(100) NOT NULL;
    ALTER TABLE [InventoryItem] ALTER COLUMN unit NVARCHAR(20);
END

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Order')
    ALTER TABLE [Order] ALTER COLUMN rejection_reason NVARCHAR(MAX);

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Delivery')
    ALTER TABLE [Delivery] ALTER COLUMN driver_name NVARCHAR(100);

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'QCInspection')
    ALTER TABLE [QCInspection] ALTER COLUMN note NVARCHAR(MAX);

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'EndOfShiftReport')
    ALTER TABLE [EndOfShiftReport] ALTER COLUMN note NVARCHAR(MAX);

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'PersonnelAssignment')
    ALTER TABLE [PersonnelAssignment] ALTER COLUMN task_description NVARCHAR(MAX);

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'BrokenEquipmentReport')
BEGIN
    ALTER TABLE [BrokenEquipmentReport] ALTER COLUMN equipment_name NVARCHAR(100) NOT NULL;
    ALTER TABLE [BrokenEquipmentReport] ALTER COLUMN description NVARCHAR(MAX);
END

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'MaintenanceRequest')
    ALTER TABLE [MaintenanceRequest] ALTER COLUMN description NVARCHAR(MAX);

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'MaterialPurchaseRequest')
    ALTER TABLE [MaterialPurchaseRequest] ALTER COLUMN item_name NVARCHAR(100) NOT NULL;

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'MaterialPurchaseRecord')
    ALTER TABLE [MaterialPurchaseRecord] ALTER COLUMN note NVARCHAR(MAX);

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'PurchaseInvoice')
    ALTER TABLE [PurchaseInvoice] ALTER COLUMN note NVARCHAR(MAX);

-- 2. Cập nhật dữ liệu tiếng Việt chuẩn có dấu (Unicode) cho bảng User
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

-- 3. Cập nhật mô tả tiếng Việt cho bảng Role
UPDATE [Role] SET description = N'Quản trị viên hệ thống' WHERE role_name = 'Admin';
UPDATE [Role] SET description = N'Chủ doanh nghiệp / Giám đốc' WHERE role_name = 'Owner';
UPDATE [Role] SET description = N'Quản lý sản xuất' WHERE role_name = 'Manager';
UPDATE [Role] SET description = N'Nhân viên sản xuất' WHERE role_name = 'Staff';
UPDATE [Role] SET description = N'Nhân viên bảo trì cơ điện' WHERE role_name = 'Maintenance Staff';
UPDATE [Role] SET description = N'Nhân viên kiểm tra chất lượng (QC)' WHERE role_name = 'QC Staff';
UPDATE [Role] SET description = N'Nhân viên mua hàng / thu mua' WHERE role_name = 'Purchasing Staff';

COMMIT TRANSACTION;
GO
