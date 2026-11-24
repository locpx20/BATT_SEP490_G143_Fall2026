-- Cho phép nhiều phiếu xuất kho (ExportSlip) cho cùng một yêu cầu xuất kho (Delivery).
-- Manager chỉ khai báo tài xế/xe; thủ kho nhập số bao khi lập phiếu và có thể lập nhiều phiếu
-- cho đến khi đủ số bao của đơn bán.
-- Chạy một lần trên database hiện có.

IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_ExportSlip_Delivery' AND parent_object_id = OBJECT_ID('dbo.ExportSlip'))
    ALTER TABLE dbo.ExportSlip DROP CONSTRAINT UQ_ExportSlip_Delivery;
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ExportSlip_delivery_id' AND object_id = OBJECT_ID('dbo.ExportSlip'))
    DROP INDEX IX_ExportSlip_delivery_id ON dbo.ExportSlip;
GO

CREATE NONCLUSTERED INDEX IX_ExportSlip_delivery_id ON dbo.ExportSlip (delivery_id);
GO
