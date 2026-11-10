USE ProductionManagementDB;
GO

-- 1. Cột giá thu mua trên bảng RawMaterial (nếu chưa có)
IF COL_LENGTH('dbo.RawMaterial', 'purchase_price') IS NULL
BEGIN
    ALTER TABLE dbo.RawMaterial ADD purchase_price DECIMAL(12, 2) NULL;
END
GO

-- 2. Cột nhân viên thu mua được giao (assigned_to) trên bảng PurchaseInvoice (nếu chưa có)
IF COL_LENGTH('dbo.PurchaseInvoice', 'assigned_to') IS NULL
BEGIN
    ALTER TABLE dbo.PurchaseInvoice ADD assigned_to INT NULL;
    ALTER TABLE dbo.PurchaseInvoice ADD CONSTRAINT FK_PurchaseInvoice_AssignedTo
        FOREIGN KEY (assigned_to) REFERENCES dbo.[User](user_id);
END
GO

-- 3. Bảng PurchaseReceipt (Phiếu mua hàng con / Yêu cầu nhập kho IR: PM-26xxx) (nếu chưa có)
IF OBJECT_ID('dbo.PurchaseReceipt', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PurchaseReceipt (
        receipt_id INT IDENTITY(1,1) PRIMARY KEY,
        receipt_code NVARCHAR(50) NOT NULL,
        invoice_id INT NOT NULL,
        supplier_id INT NOT NULL,
        weighed_quantity DECIMAL(12, 2) NOT NULL,
        actual_price DECIMAL(12, 2) NOT NULL,
        scale_photo_path NVARCHAR(300) NOT NULL,
        invoice_photo_path NVARCHAR(300) NULL,
        status NVARCHAR(50) NOT NULL CONSTRAINT DF_PurchaseReceipt_Status DEFAULT 'PendingOrderApproval',
        rejection_reason NVARCHAR(500) NULL,
        created_by INT NULL,
        created_at DATETIME NOT NULL CONSTRAINT DF_PurchaseReceipt_CreatedAt DEFAULT GETDATE(),
        CONSTRAINT FK_PurchaseReceipt_Invoice FOREIGN KEY (invoice_id) REFERENCES dbo.PurchaseInvoice(invoice_id),
        CONSTRAINT FK_PurchaseReceipt_Supplier FOREIGN KEY (supplier_id) REFERENCES dbo.Supplier(supplier_id),
        CONSTRAINT FK_PurchaseReceipt_CreatedBy FOREIGN KEY (created_by) REFERENCES dbo.[User](user_id)
    );
END
GO

-- 4. Cột lý do từ chối (rejection_reason) trên bảng PurchaseReceipt (nếu bảng đã có từ trước)
IF COL_LENGTH('dbo.PurchaseReceipt', 'rejection_reason') IS NULL
BEGIN
    ALTER TABLE dbo.PurchaseReceipt ADD rejection_reason NVARCHAR(500) NULL;
END
GO

-- 5. Các cột mở rộng trên bảng RawMaterialImport (Phiếu nhập kho nguyên liệu: PNK-26xxx & Manager phê duyệt)
IF COL_LENGTH('dbo.RawMaterialImport', 'import_code') IS NULL
BEGIN
    ALTER TABLE dbo.RawMaterialImport ADD import_code NVARCHAR(50) NULL;
END
GO

IF COL_LENGTH('dbo.RawMaterialImport', 'receipt_id') IS NULL
BEGIN
    ALTER TABLE dbo.RawMaterialImport ADD receipt_id INT NULL;
    ALTER TABLE dbo.RawMaterialImport ADD CONSTRAINT FK_RawMaterialImport_PurchaseReceipt
        FOREIGN KEY (receipt_id) REFERENCES dbo.PurchaseReceipt(receipt_id);
END
GO

IF COL_LENGTH('dbo.RawMaterialImport', 'status') IS NULL
BEGIN
    ALTER TABLE dbo.RawMaterialImport ADD status NVARCHAR(50) NOT NULL
        CONSTRAINT DF_RawMaterialImport_Status DEFAULT 'Pending';
END
GO

IF COL_LENGTH('dbo.RawMaterialImport', 'approved_by') IS NULL
BEGIN
    ALTER TABLE dbo.RawMaterialImport ADD approved_by INT NULL;
    ALTER TABLE dbo.RawMaterialImport ADD CONSTRAINT FK_RawMaterialImport_ApprovedBy
        FOREIGN KEY (approved_by) REFERENCES dbo.[User](user_id);
END
GO

IF COL_LENGTH('dbo.RawMaterialImport', 'approved_at') IS NULL
BEGIN
    ALTER TABLE dbo.RawMaterialImport ADD approved_at DATETIME NULL;
END
GO

-- 6. Cập nhật mã phiếu nhập kho (PNK-26xxx) cho các dòng đã có trước đó
UPDATE dbo.RawMaterialImport
SET import_code = 'PNK-26' + RIGHT('000' + CAST(import_id AS VARCHAR(10)), 3),
    status = 'Approved',
    approved_at = import_date
WHERE import_code IS NULL;
GO

