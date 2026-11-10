-- Phiếu tổng -> Phiếu con (phiếu mua hàng con do nhân viên thu mua nhập)
USE ProductionManagementDB;
GO

-- 1. PurchaseInvoice.assigned_to: nhân viên thu mua được phân công (created_by = người tạo phiếu tổng)
IF COL_LENGTH('PurchaseInvoice', 'assigned_to') IS NULL
BEGIN
    ALTER TABLE PurchaseInvoice ADD assigned_to INT NULL;
    ALTER TABLE PurchaseInvoice ADD CONSTRAINT FK_PurchaseInvoice_AssignedTo
        FOREIGN KEY (assigned_to) REFERENCES [User](user_id);
END
GO

-- Phiếu cũ: trước đây created_by đang lưu nhân viên được giao
UPDATE PurchaseInvoice SET assigned_to = created_by WHERE assigned_to IS NULL;
GO

-- 2. Bỏ approved_by (không còn luồng duyệt)
IF COL_LENGTH('PurchaseInvoice', 'approved_by') IS NOT NULL
BEGIN
    DECLARE @fk NVARCHAR(200) = (
        SELECT TOP 1 fk.name FROM sys.foreign_keys fk
        JOIN sys.foreign_key_columns fc ON fc.constraint_object_id = fk.object_id
        JOIN sys.columns c ON c.object_id = fc.parent_object_id AND c.column_id = fc.parent_column_id
        WHERE fk.parent_object_id = OBJECT_ID('PurchaseInvoice') AND c.name = 'approved_by');
    IF @fk IS NOT NULL EXEC('ALTER TABLE PurchaseInvoice DROP CONSTRAINT ' + @fk);
    ALTER TABLE PurchaseInvoice DROP COLUMN approved_by;
END
GO

-- 3. Phiếu con
IF OBJECT_ID('PurchaseReceipt', 'U') IS NULL
BEGIN
    CREATE TABLE PurchaseReceipt (
        receipt_id        INT IDENTITY(1,1) PRIMARY KEY,
        receipt_code      NVARCHAR(50)  NOT NULL UNIQUE,          -- PM-26xxx
        invoice_id        INT           NOT NULL REFERENCES PurchaseInvoice(invoice_id),
        supplier_id       INT           NOT NULL REFERENCES Supplier(supplier_id),
        weighed_quantity  DECIMAL(12,2) NOT NULL CHECK (weighed_quantity > 0),
        actual_price      DECIMAL(12,2) NOT NULL CHECK (actual_price > 0),
        scale_photo_path  NVARCHAR(300) NOT NULL,
        invoice_photo_path NVARCHAR(300) NULL,
        status            NVARCHAR(50)  NOT NULL DEFAULT 'Pending', -- PendingOrderApproval / OrderApproved / OrderRejected / WaitWarehouse / WaitManagerApproval / ImportRejected / Received
        rejection_reason  NVARCHAR(500) NULL,
        created_by        INT           NULL REFERENCES [User](user_id),
        created_at        DATETIME      NOT NULL DEFAULT GETDATE()
    );
END
GO

IF COL_LENGTH('PurchaseReceipt', 'rejection_reason') IS NULL
BEGIN
    ALTER TABLE PurchaseReceipt ADD rejection_reason NVARCHAR(500) NULL;
END
GO

