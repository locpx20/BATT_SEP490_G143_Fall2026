USE [ProductionManagementDB];
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.ShiftMaterialUsage', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ShiftMaterialUsage (
        UsageId INT IDENTITY PRIMARY KEY,
        ReportId INT NOT NULL REFERENCES dbo.EndOfShiftReport(report_id),
        MaterialId INT NOT NULL REFERENCES dbo.RawMaterial(material_id),
        Quantity DECIMAL(12,2) NOT NULL CHECK (Quantity > 0),
        ExportId INT NULL REFERENCES dbo.RawMaterialExport(export_id),
        CONSTRAINT UX_ShiftMaterialUsage_ReportMaterial UNIQUE (ReportId, MaterialId)
    );
END;
COMMIT;
PRINT N'Đã bổ sung tiêu hao nguyên liệu theo báo cáo ca. Không thay đổi tồn kho hoặc dữ liệu cũ.';
