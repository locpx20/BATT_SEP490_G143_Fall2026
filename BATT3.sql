-- 1. Kiểm tra và tạo Database 
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'ProductionManagementDB')
BEGIN
    CREATE DATABASE ProductionManagementDB;
END
GO

USE ProductionManagementDB;
GO

-- 2. Tự động xóa tất cả các ràng buộc khóa ngoại (Foreign Key) để tránh lỗi khi DROP bảng
DECLARE @Sql NVARCHAR(MAX) = N'';
SELECT @Sql += N'ALTER TABLE ' + QUOTENAME(TABLE_SCHEMA) + '.' + QUOTENAME(TABLE_NAME) + 
               N' DROP CONSTRAINT ' + QUOTENAME(CONSTRAINT_NAME) + ';' + CHAR(13)
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
WHERE CONSTRAINT_TYPE = 'FOREIGN KEY';

EXEC sp_executesql @Sql;
GO

-- 3. Xóa toàn bộ các bảng cũ theo thứ tự từ bảng con lên bảng cha
DROP TABLE IF EXISTS PurchaseInvoiceDetail;
DROP TABLE IF EXISTS PurchaseInvoice;
DROP TABLE IF EXISTS MaterialPurchaseRequest;
DROP TABLE IF EXISTS MaintenanceRequest;
DROP TABLE IF EXISTS BrokenEquipmentReport;
DROP TABLE IF EXISTS Machine;
DROP TABLE IF EXISTS EndOfShiftReport;
DROP TABLE IF EXISTS PersonnelAssignment;
DROP TABLE IF EXISTS QCInspection;
DROP TABLE IF EXISTS Delivery;
DROP TABLE IF EXISTS OrderDetail;
DROP TABLE IF EXISTS [Order];
DROP TABLE IF EXISTS FinishedGoodBatch;
DROP TABLE IF EXISTS RawMaterialExport;
DROP TABLE IF EXISTS RawMaterialImport;
DROP TABLE IF EXISTS RawMaterial;
DROP TABLE IF EXISTS InventoryItem;
DROP TABLE IF EXISTS Supplier;
DROP TABLE IF EXISTS Customer;
DROP TABLE IF EXISTS [User];
DROP TABLE IF EXISTS [Role];
GO

-- 4. Bảng Role (Phân quyền hệ thống)
CREATE TABLE [Role] (
    role_id INT IDENTITY(1,1) PRIMARY KEY,
    role_name NVARCHAR(50) NOT NULL UNIQUE,
    description NVARCHAR(MAX)
);
GO

-- 5. Bảng User (Người dùng / Nhân sự - Liên kết Role)
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
CREATE TABLE Customer (
    customer_id INT IDENTITY(1,1) PRIMARY KEY,
    customer_name NVARCHAR(100) NOT NULL,
    contact_person NVARCHAR(100),
    phone VARCHAR(20),
    email VARCHAR(100),
    address NVARCHAR(MAX),
    status NVARCHAR(20) DEFAULT 'Active',
    created_by INT NOT NULL,
    FOREIGN KEY (created_by) REFERENCES [User](user_id)
);
GO

-- 7. Bảng Supplier (Nhà cung cấp - Liên kết User tạo)
CREATE TABLE Supplier (
    supplier_id INT IDENTITY(1,1) PRIMARY KEY,
    supplier_name NVARCHAR(100) NOT NULL,
    contact_person NVARCHAR(100),
    phone VARCHAR(20),
    email VARCHAR(100),
    address NVARCHAR(MAX),
    status NVARCHAR(20) DEFAULT 'Active',
    created_by INT NOT NULL,
    FOREIGN KEY (created_by) REFERENCES [User](user_id)
);
GO

-- 8. Bảng InventoryItem (Kho Thành phẩm chính để Manager check tồn khi tạo đơn hàng)
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
CREATE TABLE RawMaterial (
    material_id INT IDENTITY(1,1) PRIMARY KEY,
    material_name NVARCHAR(100) NOT NULL DEFAULT N'Nguyên liệu sản xuất chính',
    unit NVARCHAR(20) NOT NULL DEFAULT 'kg', 
    current_quantity DECIMAL(12, 2) NOT NULL DEFAULT 0 
);
GO

-- 10. Bảng RawMaterialImport (Lịch sử nhập kho nguyên liệu - Liên kết RawMaterial, Supplier, User)
CREATE TABLE RawMaterialImport (
    import_id INT IDENTITY(1,1) PRIMARY KEY,
    material_id INT NOT NULL,
    supplier_id INT NOT NULL,                 
    quantity DECIMAL(12, 2) NOT NULL,         
    unit_price DECIMAL(12, 2) NOT NULL,       
    import_date DATETIME NOT NULL DEFAULT GETDATE(),
    imported_by INT NOT NULL,                 
    note NVARCHAR(MAX),
    FOREIGN KEY (material_id) REFERENCES RawMaterial(material_id),
    FOREIGN KEY (supplier_id) REFERENCES Supplier(supplier_id),
    FOREIGN KEY (imported_by) REFERENCES [User](user_id)
);
GO

-- 11. Bảng RawMaterialExport (Lịch sử xuất kho nguyên liệu - Liên kết RawMaterial, User)
CREATE TABLE RawMaterialExport (
    export_id INT IDENTITY(1,1) PRIMARY KEY,
    material_id INT NOT NULL,
    quantity DECIMAL(12, 2) NOT NULL,         
    export_date DATETIME NOT NULL DEFAULT GETDATE(),
    exported_by INT NOT NULL,                 
    note NVARCHAR(MAX),                        
    FOREIGN KEY (material_id) REFERENCES RawMaterial(material_id),
    FOREIGN KEY (exported_by) REFERENCES [User](user_id)
);
GO

-- 12. Bảng FinishedGoodBatch (Quản lý các bao thành phẩm đánh số theo ngày làm - Liên kết User)
CREATE TABLE FinishedGoodBatch (
    batch_id INT IDENTITY(1,1) PRIMARY KEY,
    bag_number NVARCHAR(50) NOT NULL UNIQUE, 
    production_date DATE NOT NULL,         
    quantity DECIMAL(12, 2) NOT NULL,      
    status NVARCHAR(50) DEFAULT 'In Stock',   
    created_by INT NOT NULL,
    FOREIGN KEY (created_by) REFERENCES [User](user_id)
);
GO

-- 13. Bảng QCInspection (Kiểm tra chất lượng - Liên kết FinishedGoodBatch và User)
CREATE TABLE QCInspection (
    inspection_id INT IDENTITY(1,1) PRIMARY KEY,
    inspection_code NVARCHAR(50) NOT NULL UNIQUE,
    batch_id INT NOT NULL,                 
    request_date DATETIME NOT NULL,
    result NVARCHAR(20),                    
    note NVARCHAR(MAX),
    status NVARCHAR(50) DEFAULT 'Pending',
    requested_by INT NOT NULL,             
    inspected_by INT,                      
    inspect_date DATETIME,
    FOREIGN KEY (batch_id) REFERENCES FinishedGoodBatch(batch_id),
    FOREIGN KEY (requested_by) REFERENCES [User](user_id),
    FOREIGN KEY (inspected_by) REFERENCES [User](user_id)
);
GO

-- 14. Bảng PersonnelAssignment (Phân công nhân sự - Liên kết User)
CREATE TABLE PersonnelAssignment (
    assignment_id INT IDENTITY(1,1) PRIMARY KEY,
    staff_id INT NOT NULL,
    assigned_by INT NOT NULL,
    assignment_date DATE NOT NULL,
    shift NVARCHAR(50),
    task_description NVARCHAR(MAX),
    status NVARCHAR(50) DEFAULT 'Assigned',
    FOREIGN KEY (staff_id) REFERENCES [User](user_id),
    FOREIGN KEY (assigned_by) REFERENCES [User](user_id)
);
GO

-- 15. Bảng EndOfShiftReport (Báo cáo cuối ca - Liên kết PersonnelAssignment và User)
CREATE TABLE EndOfShiftReport (
    report_id INT IDENTITY(1,1) PRIMARY KEY,
    assignment_id INT NOT NULL,          
    staff_id INT NOT NULL,
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
CREATE TABLE Machine (
    machine_id INT IDENTITY(1,1) PRIMARY KEY,
    machine_code NVARCHAR(50) NOT NULL UNIQUE,
    machine_name NVARCHAR(100) NOT NULL,
    process_name NVARCHAR(100) NOT NULL, -- Công đoạn (lưu dạng text)
    brand NVARCHAR(100),                 -- Hãng máy
    created_by INT NOT NULL,             -- Nhân sự (Manager/Admin) thêm máy vào hệ thống
    FOREIGN KEY (created_by) REFERENCES [User](user_id)
);
GO

-- 17. Bảng BrokenEquipmentReport (Báo cáo thiết bị hỏng - Liên kết Machine và User)
CREATE TABLE BrokenEquipmentReport (
    report_id INT IDENTITY(1,1) PRIMARY KEY,
    machine_id INT NOT NULL,             -- Liên kết tới máy móc bị hỏng
    description NVARCHAR(MAX),           -- Mô tả chi tiết lỗi hỏng
    reported_by INT NOT NULL,            -- Nhân viên báo cáo
    report_date DATETIME NOT NULL,
    status NVARCHAR(50) DEFAULT 'Reported',
    severity NVARCHAR(20),               -- Mức độ nghiêm trọng
    FOREIGN KEY (machine_id) REFERENCES Machine(machine_id),
    FOREIGN KEY (reported_by) REFERENCES [User](user_id)
);
GO

-- 18. Bảng MaintenanceRequest (Yêu cầu bảo trì - Liên kết BrokenEquipmentReport và User)
CREATE TABLE MaintenanceRequest (
    request_id INT IDENTITY(1,1) PRIMARY KEY,
    broken_report_id INT NOT NULL,
    requested_by INT NOT NULL,
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
CREATE TABLE MaterialPurchaseRequest (
    request_id INT IDENTITY(1,1) PRIMARY KEY,
    maintenance_request_id INT NOT NULL,
    item_name NVARCHAR(100) NOT NULL,
    quantity INT NOT NULL,
    requested_by INT NOT NULL,
    status NVARCHAR(50) DEFAULT 'Pending',
    request_date DATETIME NOT NULL,
    approved_by INT,
    FOREIGN KEY (maintenance_request_id) REFERENCES MaintenanceRequest(request_id),
    FOREIGN KEY (requested_by) REFERENCES [User](user_id),
    FOREIGN KEY (approved_by) REFERENCES [User](user_id)
);
GO

-- 20. Bảng Order (Đơn hàng - Liên kết Customer, InventoryItem, User)
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
    created_by INT NOT NULL,              
    order_date DATETIME NOT NULL,
    FOREIGN KEY (customer_id) REFERENCES Customer(customer_id),
    FOREIGN KEY (inventory_id) REFERENCES InventoryItem(inventory_id),
    FOREIGN KEY (approved_by) REFERENCES [User](user_id),
    FOREIGN KEY (created_by) REFERENCES [User](user_id)
);
GO

-- 21. Bảng OrderDetail (Chi tiết đơn hàng xuất kho thành phẩm từ FinishedGoodBatch)
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
CREATE TABLE Delivery (
    delivery_id INT IDENTITY(1,1) PRIMARY KEY,
    order_id INT NOT NULL,
    license_plate NVARCHAR(20) NOT NULL,
    driver_name NVARCHAR(100) NOT NULL,
    pickup_date DATETIME NOT NULL,
    delivery_status NVARCHAR(50) DEFAULT 'Prepared',
    confirmed_by INT NOT NULL, 
    FOREIGN KEY (order_id) REFERENCES [Order](order_id),
    FOREIGN KEY (confirmed_by) REFERENCES [User](user_id)
);
GO

-- 23. Bảng PurchaseInvoice (Hóa đơn mua hàng từ nhà cung cấp - Liên kết Supplier và User)
CREATE TABLE PurchaseInvoice (
    invoice_id INT IDENTITY(1,1) PRIMARY KEY,
    invoice_code NVARCHAR(50) NOT NULL UNIQUE,
    supplier_id INT NOT NULL,
    purchase_date DATETIME NOT NULL,
    total_amount DECIMAL(12, 2) NOT NULL,
    status NVARCHAR(50) DEFAULT 'Pending',
    created_by INT NOT NULL,
    approved_by INT,
    note NVARCHAR(MAX),
    FOREIGN KEY (supplier_id) REFERENCES Supplier(supplier_id),
    FOREIGN KEY (created_by) REFERENCES [User](user_id),
    FOREIGN KEY (approved_by) REFERENCES [User](user_id)
);
GO

-- 24. Bảng PurchaseInvoiceDetail (Chi tiết hóa đơn mua hàng - Liên kết PurchaseInvoice và RawMaterial)
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
