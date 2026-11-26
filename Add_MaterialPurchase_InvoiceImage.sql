-- =====================================================================
-- Add_MaterialPurchase_InvoiceImage.sql
-- Bổ sung trường invoice_image cho MaterialPurchaseRequest (hóa đơn mua vật tư bảo trì)
-- =====================================================================
SET NOCOUNT ON;
GO

IF COL_LENGTH(N'dbo.MaterialPurchaseRequest', N'invoice_image') IS NULL
BEGIN
    ALTER TABLE dbo.MaterialPurchaseRequest ADD invoice_image NVARCHAR(300) NULL;
    PRINT N'Đã thêm cột invoice_image vào bảng MaterialPurchaseRequest.';
END
ELSE
BEGIN
    PRINT N'Cột invoice_image đã tồn tại.';
END
GO
