USE ProductionManagementDB;
GO

-- 1. Tạo bảng InboundRequest (Yêu cầu nhập kho: IR-26xxx) tách riêng khỏi PurchaseReceipt (Phiếu mua hàng / Purchase Order: PM-26xxx)
--    1 PurchaseReceipt (PM-26xxx) có thể có nhiều InboundRequest (IR-26xxx) tương ứng với nhiều chuyến xe / đợt giao hàng.
IF OBJECT_ID('dbo.InboundRequest', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.InboundRequest (
        inbound_request_id INT IDENTITY(1,1) PRIMARY KEY,
        request_code       NVARCHAR(50)  NOT NULL UNIQUE,          -- IR-26xxx
        receipt_id         INT           NOT NULL,                 -- FK -> PurchaseReceipt (PM-26xxx)
        invoice_id         INT           NOT NULL,                 -- FK -> PurchaseInvoice (TM-26xxx)
        supplier_id        INT           NOT NULL,                 -- FK -> Supplier
        delivery_quantity  DECIMAL(12,2) NOT NULL CHECK (delivery_quantity > 0),
        unit_price         DECIMAL(12,2) NOT NULL CHECK (unit_price > 0),
        license_plate      NVARCHAR(30)  NOT NULL,
        driver_name        NVARCHAR(100) NOT NULL,
        delivery_time      DATETIME      NOT NULL,
        scale_photo_path   NVARCHAR(300) NULL,
        invoice_photo_path NVARCHAR(300) NULL,
        status             NVARCHAR(50)  NOT NULL CONSTRAINT DF_InboundRequest_Status DEFAULT 'WaitWarehouse', -- WaitWarehouse / WaitManagerApproval / ImportRejected / Received
        rejection_reason   NVARCHAR(500) NULL,
        note               NVARCHAR(500) NULL,
        created_by         INT           NULL,
        created_at         DATETIME      NOT NULL CONSTRAINT DF_InboundRequest_CreatedAt DEFAULT GETDATE(),
        CONSTRAINT FK_InboundRequest_Receipt FOREIGN KEY (receipt_id) REFERENCES dbo.PurchaseReceipt(receipt_id),
        CONSTRAINT FK_InboundRequest_Invoice FOREIGN KEY (invoice_id) REFERENCES dbo.PurchaseInvoice(invoice_id),
        CONSTRAINT FK_InboundRequest_Supplier FOREIGN KEY (supplier_id) REFERENCES dbo.Supplier(supplier_id),
        CONSTRAINT FK_InboundRequest_CreatedBy FOREIGN KEY (created_by) REFERENCES dbo.[User](user_id)
    );
END
GO

-- 2. Thêm cột inbound_request_id trên bảng RawMaterialImport (Phiếu nhập kho nguyên liệu: PNK-26xxx)
IF COL_LENGTH('dbo.RawMaterialImport', 'inbound_request_id') IS NULL
BEGIN
    ALTER TABLE dbo.RawMaterialImport ADD inbound_request_id INT NULL;
    ALTER TABLE dbo.RawMaterialImport ADD CONSTRAINT FK_RawMaterialImport_InboundRequest
        FOREIGN KEY (inbound_request_id) REFERENCES dbo.InboundRequest(inbound_request_id);
END
GO

-- 3. Di chuyển dữ liệu cũ: các PurchaseReceipt đã chuyển sang bước kho (WaitWarehouse, Pending, WaitManagerApproval, ImportRejected, Received)
--    mà chưa có bản ghi trong InboundRequest thì tự động sinh 1 bản ghi InboundRequest (IR-26xxx) tương ứng.
INSERT INTO dbo.InboundRequest (
    request_code,
    receipt_id,
    invoice_id,
    supplier_id,
    delivery_quantity,
    unit_price,
    license_plate,
    driver_name,
    delivery_time,
    scale_photo_path,
    invoice_photo_path,
    status,
    rejection_reason,
    note,
    created_by,
    created_at
)
SELECT
    'IR-26' + RIGHT('000' + CAST(r.receipt_id AS VARCHAR(10)), 3) AS request_code,
    r.receipt_id,
    r.invoice_id,
    r.supplier_id,
    r.weighed_quantity,
    r.actual_price,
    N'72C-184.92' AS license_plate,
    N'Nguyễn Văn Tài' AS driver_name,
    r.created_at AS delivery_time,
    r.scale_photo_path,
    r.invoice_photo_path,
    CASE
        WHEN r.status IN ('WaitWarehouse', 'Pending', 'WaitManagerApproval', 'ImportRejected', 'Received') THEN r.status
        ELSE 'WaitWarehouse'
    END AS status,
    CASE
        WHEN r.status = 'ImportRejected' THEN r.rejection_reason
        ELSE NULL
    END AS rejection_reason,
    N'Yêu cầu nhập kho từ phiếu mua hàng ' + r.receipt_code AS note,
    r.created_by,
    r.created_at
FROM dbo.PurchaseReceipt r
WHERE r.status IN ('WaitWarehouse', 'Pending', 'WaitManagerApproval', 'ImportRejected', 'Received')
  AND NOT EXISTS (
      SELECT 1 FROM dbo.InboundRequest ir WHERE ir.receipt_id = r.receipt_id
  );
GO

-- 4. Liên kết các RawMaterialImport (PNK-26xxx) cũ với InboundRequest tương ứng qua receipt_id
UPDATE imp
SET imp.inbound_request_id = ir.inbound_request_id
FROM dbo.RawMaterialImport imp
INNER JOIN dbo.InboundRequest ir ON ir.receipt_id = imp.receipt_id
WHERE imp.inbound_request_id IS NULL;
GO

-- 5. Chuẩn hóa lại trạng thái của PurchaseReceipt (Purchase Order):
--    PurchaseReceipt chỉ giữ các trạng thái của đơn mua: PendingOrderApproval, OrderRejected, OrderApproved, Received
UPDATE dbo.PurchaseReceipt
SET status = 'OrderApproved',
    rejection_reason = NULL
WHERE status IN ('WaitWarehouse', 'Pending', 'WaitManagerApproval', 'ImportRejected');
GO

-- 6. Đảm bảo phông chữ Unicode tiếng Việt chuẩn cho các bản ghi InboundRequest đã di chuyển
UPDATE ir
SET ir.driver_name = N'Nguyễn Văn Tài',
    ir.note = N'Yêu cầu nhập kho từ phiếu mua hàng ' + r.receipt_code
FROM dbo.InboundRequest ir
INNER JOIN dbo.PurchaseReceipt r ON r.receipt_id = ir.receipt_id
WHERE ir.driver_name LIKE N'%Nguy%' AND ir.driver_name <> N'Nguyễn Văn Tài';
GO


