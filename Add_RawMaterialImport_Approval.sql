USE ProductionManagementDB;
GO

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

-- Cập nhật mã phiếu nhập kho (PNK-26xxx) cho các dòng đã có trước đó
UPDATE dbo.RawMaterialImport
SET import_code = 'PNK-26' + RIGHT('000' + CAST(import_id AS VARCHAR(10)), 3),
    status = 'Approved',
    approved_at = import_date
WHERE import_code IS NULL;
GO

