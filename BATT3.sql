-- 1. Kiểm tra và tạo Database 
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'ProductionManagementDB')
BEGIN
    CREATE DATABASE ProductionManagementDB;
END
GO

USE ProductionManagementDB;
GO

-- Chỉ tạo bảng chưa có. Không xóa bảng, ràng buộc hoặc dữ liệu hiện tại.

-- 4. Bảng Role (Phân quyền hệ thống)
IF OBJECT_ID(N'dbo.Role', N'U') IS NULL
CREATE TABLE [Role] (
    role_id INT IDENTITY(1,1) PRIMARY KEY,
    role_name NVARCHAR(50) NOT NULL UNIQUE,
    description NVARCHAR(MAX)
);
GO

-- 5. Bảng User (Người dùng / Nhân sự - Liên kết Role)
IF OBJECT_ID(N'dbo.User', N'U') IS NULL
CREATE TABLE [User] (
    user_id INT IDENTITY(1,1) PRIMARY KEY,
    created_at DATETIME2(0) NULL CONSTRAINT DF_User_CreatedAt DEFAULT SYSUTCDATETIME(),
    username NVARCHAR(50) NOT NULL UNIQUE,
    password VARCHAR(255) NOT NULL,
    full_name NVARCHAR(100) NOT NULL,
    status NVARCHAR(20) DEFAULT 'Active',
    phone VARCHAR(20),
    email VARCHAR(100),
    role_id INT NOT NULL,
    FOREIGN KEY (role_id) REFERENCES [Role](role_id)
);
GO

-- 6. Bảng Customer (Khách hàng - Liên kết User tạo)
IF OBJECT_ID(N'dbo.Customer', N'U') IS NULL
CREATE TABLE Customer (
    customer_id INT IDENTITY(1,1) PRIMARY KEY,
    customer_name NVARCHAR(100) NOT NULL,
    contact_person NVARCHAR(100),
    phone VARCHAR(20),
    email VARCHAR(100),
    address NVARCHAR(MAX),
    status NVARCHAR(20) DEFAULT 'Active',
    created_by INT NULL,
    FOREIGN KEY (created_by) REFERENCES [User](user_id)
);
GO

-- 7. Bảng Supplier (Nhà cung cấp - Liên kết User tạo)
IF OBJECT_ID(N'dbo.Supplier', N'U') IS NULL
CREATE TABLE Supplier (
    supplier_id INT IDENTITY(1,1) PRIMARY KEY,
    supplier_name NVARCHAR(100) NOT NULL,
    contact_person NVARCHAR(100),
    phone VARCHAR(20),
    email VARCHAR(100),
    address NVARCHAR(MAX),
    status NVARCHAR(20) DEFAULT 'Active',
    created_by INT NULL,
    FOREIGN KEY (created_by) REFERENCES [User](user_id)
);
GO

-- 8. Bảng InventoryItem (Kho Thành phẩm chính để Manager check tồn khi tạo đơn hàng)
IF OBJECT_ID(N'dbo.InventoryItem', N'U') IS NULL
CREATE TABLE InventoryItem (
    inventory_id INT IDENTITY(1,1) PRIMARY KEY,
    item_code NVARCHAR(50) NOT NULL UNIQUE,
    item_name NVARCHAR(100) NOT NULL,
    unit NVARCHAR(20) NOT NULL,
    quantity INT NOT NULL DEFAULT 0,
    min_quantity INT NOT NULL DEFAULT 0,
    status NVARCHAR(20) DEFAULT 'Available'
);
GO

-- 9. Bảng RawMaterial (Kho Nguyên liệu duy nhất)
IF OBJECT_ID(N'dbo.RawMaterial', N'U') IS NULL
CREATE TABLE RawMaterial (
    material_id INT IDENTITY(1,1) PRIMARY KEY,
    material_name NVARCHAR(100) NOT NULL DEFAULT N'Nguyên liệu sản xuất chính',
    unit NVARCHAR(20) NOT NULL DEFAULT 'kg', 
    current_quantity DECIMAL(12, 2) NOT NULL DEFAULT 0 
);
GO

-- 10. Bảng RawMaterialImport (Lịch sử nhập kho nguyên liệu - Liên kết RawMaterial, Supplier, User)
IF OBJECT_ID(N'dbo.RawMaterialImport', N'U') IS NULL
CREATE TABLE RawMaterialImport (
    import_id INT IDENTITY(1,1) PRIMARY KEY,
    material_id INT NOT NULL,
    supplier_id INT NOT NULL,                 
    quantity DECIMAL(12, 2) NOT NULL,         
    unit_price DECIMAL(12, 2) NOT NULL,       
    import_date DATETIME NOT NULL DEFAULT GETDATE(),
    imported_by INT NULL,
    note NVARCHAR(MAX),
    FOREIGN KEY (material_id) REFERENCES RawMaterial(material_id),
    FOREIGN KEY (supplier_id) REFERENCES Supplier(supplier_id),
    FOREIGN KEY (imported_by) REFERENCES [User](user_id)
);
GO

-- 11. Bảng RawMaterialExport (Lịch sử xuất kho nguyên liệu - Liên kết RawMaterial, User)
IF OBJECT_ID(N'dbo.RawMaterialExport', N'U') IS NULL
CREATE TABLE RawMaterialExport (
    export_id INT IDENTITY(1,1) PRIMARY KEY,
    material_id INT NOT NULL,
    quantity DECIMAL(12, 2) NOT NULL,         
    export_date DATETIME NOT NULL DEFAULT GETDATE(),
    exported_by INT NULL,
    note NVARCHAR(MAX),                        
    FOREIGN KEY (material_id) REFERENCES RawMaterial(material_id),
    FOREIGN KEY (exported_by) REFERENCES [User](user_id)
);
GO

-- 12. Bảng FinishedGoodBatch (Quản lý các bao thành phẩm đánh số theo ngày làm - Liên kết User)
IF OBJECT_ID(N'dbo.FinishedGoodBatch', N'U') IS NULL
CREATE TABLE FinishedGoodBatch (
    batch_id INT IDENTITY(1,1) PRIMARY KEY,
    bag_number NVARCHAR(50) NOT NULL UNIQUE, 
    production_date DATE NOT NULL,         
    quantity DECIMAL(12, 2) NOT NULL,      
    status NVARCHAR(50) DEFAULT 'In Stock',   
    created_by INT NULL,
    FOREIGN KEY (created_by) REFERENCES [User](user_id)
);
GO

-- 13. Bảng QCInspection (Kiểm tra chất lượng - Liên kết FinishedGoodBatch và User)
IF OBJECT_ID(N'dbo.QCInspection', N'U') IS NULL
CREATE TABLE QCInspection (
    inspection_id INT IDENTITY(1,1) PRIMARY KEY,
    inspection_code NVARCHAR(50) NOT NULL UNIQUE,
    batch_id INT NOT NULL,                 
    request_date DATETIME NOT NULL,
    result NVARCHAR(20),                    
    note NVARCHAR(MAX),
    status NVARCHAR(50) DEFAULT 'Pending',
    requested_by INT NULL,
    inspected_by INT,                      
    inspect_date DATETIME,
    FOREIGN KEY (batch_id) REFERENCES FinishedGoodBatch(batch_id),
    FOREIGN KEY (requested_by) REFERENCES [User](user_id),
    FOREIGN KEY (inspected_by) REFERENCES [User](user_id)
);
GO

-- 14. Bảng PersonnelAssignment (Phân công nhân sự - Liên kết User)
IF OBJECT_ID(N'dbo.PersonnelAssignment', N'U') IS NULL
CREATE TABLE PersonnelAssignment (
    assignment_id INT IDENTITY(1,1) PRIMARY KEY,
    staff_id INT NULL,
    assigned_by INT NULL,
    assignment_date DATE NOT NULL,
    shift NVARCHAR(50),
    task_description NVARCHAR(MAX),
    status NVARCHAR(50) DEFAULT 'Assigned',
    FOREIGN KEY (staff_id) REFERENCES [User](user_id),
    FOREIGN KEY (assigned_by) REFERENCES [User](user_id)
);
GO

-- 15. Bảng EndOfShiftReport (Báo cáo cuối ca - Liên kết PersonnelAssignment và User)
IF OBJECT_ID(N'dbo.EndOfShiftReport', N'U') IS NULL
CREATE TABLE EndOfShiftReport (
    report_id INT IDENTITY(1,1) PRIMARY KEY,
    assignment_id INT NOT NULL,          
    staff_id INT NULL,
    shift_date DATE NOT NULL,
    production_quantity INT NOT NULL,
    note NVARCHAR(MAX),
    status NVARCHAR(50) DEFAULT 'Submitted',
    created_at DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (assignment_id) REFERENCES PersonnelAssignment(assignment_id),
    FOREIGN KEY (staff_id) REFERENCES [User](user_id)
);
GO

-- 16. Bảng Machine (Quản lý máy móc thiết bị trong nhà máy)
-- Bảng Machine (Quản lý máy móc thiết bị - Đã liên kết với User người tạo)
IF OBJECT_ID(N'dbo.Machine', N'U') IS NULL
CREATE TABLE Machine (
    machine_id INT IDENTITY(1,1) PRIMARY KEY,
    machine_code NVARCHAR(50) NOT NULL UNIQUE,
    machine_name NVARCHAR(100) NOT NULL,
    process_name NVARCHAR(100) NOT NULL, -- Công đoạn (lưu dạng text)
    brand NVARCHAR(100),                 -- Hãng máy
    created_by INT NULL,             -- Nhân sự (Manager/Admin) thêm máy vào hệ thống
    FOREIGN KEY (created_by) REFERENCES [User](user_id)
);
GO

-- 17. Bảng BrokenEquipmentReport (Báo cáo thiết bị hỏng - Liên kết Machine và User)
IF OBJECT_ID(N'dbo.BrokenEquipmentReport', N'U') IS NULL
CREATE TABLE BrokenEquipmentReport (
    report_id INT IDENTITY(1,1) PRIMARY KEY,
    machine_id INT NOT NULL,             -- Liên kết tới máy móc bị hỏng
    description NVARCHAR(MAX),           -- Mô tả chi tiết lỗi hỏng
    reported_by INT NULL,            -- Nhân viên báo cáo
    report_date DATETIME NOT NULL,
    status NVARCHAR(50) DEFAULT 'Reported',
    severity NVARCHAR(20),               -- Mức độ nghiêm trọng
    FOREIGN KEY (machine_id) REFERENCES Machine(machine_id),
    FOREIGN KEY (reported_by) REFERENCES [User](user_id)
);
GO

-- 18. Bảng MaintenanceRequest (Yêu cầu bảo trì - Liên kết BrokenEquipmentReport và User)
IF OBJECT_ID(N'dbo.MaintenanceRequest', N'U') IS NULL
CREATE TABLE MaintenanceRequest (
    request_id INT IDENTITY(1,1) PRIMARY KEY,
    broken_report_id INT NOT NULL,
    requested_by INT NULL,
    assigned_to INT,
    request_date DATETIME NOT NULL,
    status NVARCHAR(50) DEFAULT 'Pending',
    priority NVARCHAR(20),
    description NVARCHAR(MAX),
    FOREIGN KEY (broken_report_id) REFERENCES BrokenEquipmentReport(report_id),
    FOREIGN KEY (requested_by) REFERENCES [User](user_id),
    FOREIGN KEY (assigned_to) REFERENCES [User](user_id)
);
GO

-- 19. Bảng MaterialPurchaseRequest (Yêu cầu mua vật tư sửa chữa - Liên kết MaintenanceRequest và User)
IF OBJECT_ID(N'dbo.MaterialPurchaseRequest', N'U') IS NULL
CREATE TABLE MaterialPurchaseRequest (
    request_id INT IDENTITY(1,1) PRIMARY KEY,
    maintenance_request_id INT NOT NULL,
    item_name NVARCHAR(100) NOT NULL,
    quantity INT NOT NULL,
    requested_by INT NULL,
    status NVARCHAR(50) DEFAULT 'Pending',
    request_date DATETIME NOT NULL,
    approved_by INT,
    FOREIGN KEY (maintenance_request_id) REFERENCES MaintenanceRequest(request_id),
    FOREIGN KEY (requested_by) REFERENCES [User](user_id),
    FOREIGN KEY (approved_by) REFERENCES [User](user_id)
);
GO

-- 20. Bảng Order (Đơn hàng - Liên kết Customer, InventoryItem, User)
IF OBJECT_ID(N'dbo.Order', N'U') IS NULL
CREATE TABLE [Order] (
    order_id INT IDENTITY(1,1) PRIMARY KEY,
    order_code NVARCHAR(50) NOT NULL UNIQUE,
    customer_id INT NOT NULL,
    inventory_id INT NOT NULL,            
    quantity INT NOT NULL,
    unit_price DECIMAL(12, 2) NOT NULL,
    total_amount DECIMAL(12, 2) NOT NULL,
    status NVARCHAR(50) DEFAULT 'Pending', 
    rejection_reason NVARCHAR(MAX),
    approved_by INT,
    created_by INT NULL,
    order_date DATETIME NOT NULL,
    FOREIGN KEY (customer_id) REFERENCES Customer(customer_id),
    FOREIGN KEY (inventory_id) REFERENCES InventoryItem(inventory_id),
    FOREIGN KEY (approved_by) REFERENCES [User](user_id),
    FOREIGN KEY (created_by) REFERENCES [User](user_id)
);
GO

-- 21. Bảng OrderDetail (Chi tiết đơn hàng xuất kho thành phẩm từ FinishedGoodBatch)
IF OBJECT_ID(N'dbo.OrderDetail', N'U') IS NULL
CREATE TABLE OrderDetail (
    order_detail_id INT IDENTITY(1,1) PRIMARY KEY,
    order_id INT NOT NULL,
    batch_id INT NOT NULL, 
    quantity DECIMAL(12, 2) NOT NULL,
    FOREIGN KEY (order_id) REFERENCES [Order](order_id),
    FOREIGN KEY (batch_id) REFERENCES FinishedGoodBatch(batch_id)
);
GO

-- 22. Bảng Delivery (Thông tin giao hàng - Liên kết Order và User)
IF OBJECT_ID(N'dbo.Delivery', N'U') IS NULL
CREATE TABLE Delivery (
    delivery_id INT IDENTITY(1,1) PRIMARY KEY,
    order_id INT NOT NULL,
    license_plate NVARCHAR(20) NOT NULL,
    driver_name NVARCHAR(100) NOT NULL,
    pickup_date DATETIME NOT NULL,
    delivery_status NVARCHAR(50) DEFAULT 'Prepared',
    confirmed_by INT NULL,
    FOREIGN KEY (order_id) REFERENCES [Order](order_id),
    FOREIGN KEY (confirmed_by) REFERENCES [User](user_id)
);
GO

-- 23. Bảng PurchaseInvoice (Hóa đơn mua hàng từ nhà cung cấp - Liên kết Supplier và User)
IF OBJECT_ID(N'dbo.PurchaseInvoice', N'U') IS NULL
CREATE TABLE PurchaseInvoice (
    invoice_id INT IDENTITY(1,1) PRIMARY KEY,
    invoice_code NVARCHAR(50) NOT NULL UNIQUE,
    supplier_id INT NOT NULL,
    purchase_date DATETIME NOT NULL,
    total_amount DECIMAL(12, 2) NOT NULL,
    status NVARCHAR(50) DEFAULT 'Pending',
    created_by INT NULL,
    approved_by INT,
    note NVARCHAR(MAX),
    FOREIGN KEY (supplier_id) REFERENCES Supplier(supplier_id),
    FOREIGN KEY (created_by) REFERENCES [User](user_id),
    FOREIGN KEY (approved_by) REFERENCES [User](user_id)
);
GO

-- 24. Bảng PurchaseInvoiceDetail (Chi tiết hóa đơn mua hàng - Liên kết PurchaseInvoice và RawMaterial)
IF OBJECT_ID(N'dbo.PurchaseInvoiceDetail', N'U') IS NULL
CREATE TABLE PurchaseInvoiceDetail (
    invoice_detail_id INT IDENTITY(1,1) PRIMARY KEY,
    invoice_id INT NOT NULL,
    material_id INT NOT NULL, 
    quantity DECIMAL(12, 2) NOT NULL,
    unit_price DECIMAL(12, 2) NOT NULL,
    FOREIGN KEY (invoice_id) REFERENCES PurchaseInvoice(invoice_id),
    FOREIGN KEY (material_id) REFERENCES RawMaterial(material_id)
);
GO

-- =====================================================================
-- CẬP NHẬT BẢO TRÌ CHO DATABASE ĐÃ CÓ DỮ LIỆU
-- Phần này chạy lại được; chỉ bổ sung cấu trúc còn thiếu.
-- =====================================================================
-- Đồng bộ schema bảo trì với model hiện tại.
-- Chi them cot/bang con thieu; khong xoa, tao lai bang hoac sua noi dung du lieu cu.
USE [ProductionManagementDB];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.MaintenanceRequest', N'U') IS NULL
    THROW 50001, N'Không tìm thấy dbo.MaintenanceRequest. Kiểm tra database đang chọn.', 1;
IF OBJECT_ID(N'dbo.User', N'U') IS NULL
    THROW 50002, N'Không tìm thấy dbo.User. Kiểm tra database đang chọn.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.MaintenanceRequest', N'title') IS NULL
        ALTER TABLE dbo.MaintenanceRequest ADD title NVARCHAR(150) NULL;
    IF COL_LENGTH(N'dbo.MaintenanceRequest', N'location') IS NULL
        ALTER TABLE dbo.MaintenanceRequest ADD location NVARCHAR(200) NULL;
    IF COL_LENGTH(N'dbo.MaintenanceRequest', N'machine_stopped') IS NULL
        ALTER TABLE dbo.MaintenanceRequest ADD machine_stopped BIT NOT NULL
            CONSTRAINT DF_MaintenanceRequest_machine_stopped DEFAULT (0) WITH VALUES;
    IF COL_LENGTH(N'dbo.MaintenanceRequest', N'rejection_reason') IS NULL
        ALTER TABLE dbo.MaintenanceRequest ADD rejection_reason NVARCHAR(500) NULL;
    IF COL_LENGTH(N'dbo.MaintenanceRequest', N'due_date') IS NULL
        ALTER TABLE dbo.MaintenanceRequest ADD due_date DATE NULL;

    -- Cac cot cung duoc model bao tri hien tai su dung.
    IF COL_LENGTH(N'dbo.MaintenanceRequest', N'reviewed_by') IS NULL
        ALTER TABLE dbo.MaintenanceRequest ADD reviewed_by INT NULL;
    IF COL_LENGTH(N'dbo.MaintenanceRequest', N'reviewed_at') IS NULL
        ALTER TABLE dbo.MaintenanceRequest ADD reviewed_at DATETIME NULL;
    IF COL_LENGTH(N'dbo.MaintenanceRequest', N'assignment_note') IS NULL
        ALTER TABLE dbo.MaintenanceRequest ADD assignment_note NVARCHAR(500) NULL;
    IF COL_LENGTH(N'dbo.MaintenanceRequest', N'assigned_at') IS NULL
        ALTER TABLE dbo.MaintenanceRequest ADD assigned_at DATETIME NULL;
    IF COL_LENGTH(N'dbo.MaintenanceRequest', N'confirmed_at') IS NULL
        ALTER TABLE dbo.MaintenanceRequest ADD confirmed_at DATETIME NULL;
    IF COL_LENGTH(N'dbo.MaintenanceRequest', N'completed_at') IS NULL
        ALTER TABLE dbo.MaintenanceRequest ADD completed_at DATETIME NULL;
    IF COL_LENGTH(N'dbo.MaintenanceRequest', N'completion_note') IS NULL
        ALTER TABLE dbo.MaintenanceRequest ADD completion_note NVARCHAR(500) NULL;

    IF NOT EXISTS (
        SELECT 1 FROM sys.foreign_key_columns
        WHERE parent_object_id = OBJECT_ID(N'dbo.MaintenanceRequest')
          AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.MaintenanceRequest'), N'reviewed_by', 'ColumnId')
          AND referenced_object_id = OBJECT_ID(N'dbo.User')
    )
        EXEC sys.sp_executesql N'ALTER TABLE dbo.MaintenanceRequest WITH CHECK
            ADD CONSTRAINT FK_MR_reviewed_by FOREIGN KEY (reviewed_by) REFERENCES dbo.[User](user_id);';

    IF OBJECT_ID(N'dbo.MaintenanceRequestLog', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.MaintenanceRequestLog (
            log_id INT IDENTITY(1,1) CONSTRAINT PK_MaintenanceRequestLog PRIMARY KEY,
            request_id INT NOT NULL,
            action NVARCHAR(30) NOT NULL,
            from_status NVARCHAR(50) NULL,
            to_status NVARCHAR(50) NULL,
            actor_id INT NOT NULL,
            note NVARCHAR(500) NULL,
            created_at DATETIME NOT NULL,
            CONSTRAINT FK_MRL_Request FOREIGN KEY (request_id) REFERENCES dbo.MaintenanceRequest(request_id),
            CONSTRAINT FK_MRL_Actor FOREIGN KEY (actor_id) REFERENCES dbo.[User](user_id)
        );
    END;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MaintenanceRequestLog') AND name = N'IX_MRL_request_created')
        CREATE INDEX IX_MRL_request_created ON dbo.MaintenanceRequestLog(request_id, created_at DESC);

    COMMIT TRANSACTION;
    PRINT N'Đã cập nhật MaintenanceRequest và nhật ký bảo trì. Tiếp tục cập nhật vật tư và thông báo trong file này.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- =====================================================================
-- Add_MaintenanceMaterialFlow.sql
-- Luồng mua vật tư trong bảo trì:
--   Nhân viên bảo trì tự kiểm tra vật tư. Đủ thì sửa tiếp; thiếu thì tạo yêu cầu mua
--   -> Manager mua xong -> thông báo -> nhân viên sửa tiếp.
-- Thêm:
--   1) MaterialPurchaseRequest : bổ sung cột đơn vị, ghi chú, kết quả mua
--   2) AppNotification         : thông báo nội bộ gửi tới người dùng
-- Trạng thái mới của MaintenanceRequest: 'WaitingMaterial' (không cần đổi schema).
-- Chạy được nhiều lần (idempotent). NÊN SAO LƯU DB TRƯỚC KHI CHẠY.
-- =====================================================================
SET NOCOUNT ON;
GO

IF COL_LENGTH(N'dbo.MaterialPurchaseRequest', N'unit') IS NULL
    ALTER TABLE dbo.MaterialPurchaseRequest ADD unit NVARCHAR(20) NULL;
GO
IF COL_LENGTH(N'dbo.MaterialPurchaseRequest', N'note') IS NULL
    ALTER TABLE dbo.MaterialPurchaseRequest ADD note NVARCHAR(500) NULL;          -- ghi chú của nhân viên bảo trì
GO
IF COL_LENGTH(N'dbo.MaterialPurchaseRequest', N'result_note') IS NULL
    ALTER TABLE dbo.MaterialPurchaseRequest ADD result_note NVARCHAR(500) NULL;   -- ghi chú mua hàng / lý do từ chối của Manager
GO
IF COL_LENGTH(N'dbo.MaterialPurchaseRequest', N'total_cost') IS NULL
    ALTER TABLE dbo.MaterialPurchaseRequest ADD total_cost DECIMAL(14, 2) NULL;
GO
IF COL_LENGTH(N'dbo.MaterialPurchaseRequest', N'handled_at') IS NULL
    ALTER TABLE dbo.MaterialPurchaseRequest ADD handled_at DATETIME NULL;         -- thời điểm Manager xử lý
GO

IF OBJECT_ID(N'dbo.AppNotification', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppNotification (
        notification_id INT IDENTITY(1,1) PRIMARY KEY,
        user_id         INT NOT NULL,
        title           NVARCHAR(150) NOT NULL,
        message         NVARCHAR(500) NOT NULL,
        url             NVARCHAR(200) NULL,
        is_read         BIT NOT NULL CONSTRAINT DF_AppNotification_read DEFAULT 0,
        created_at      DATETIME NOT NULL CONSTRAINT DF_AppNotification_created DEFAULT GETDATE(),
        CONSTRAINT FK_AppNotification_User FOREIGN KEY (user_id) REFERENCES dbo.[User](user_id)
    );
    CREATE INDEX IX_AppNotification_user_unread ON dbo.AppNotification(user_id, is_read, created_at DESC);
END
GO

PRINT N'Hoàn tất.';
GO
