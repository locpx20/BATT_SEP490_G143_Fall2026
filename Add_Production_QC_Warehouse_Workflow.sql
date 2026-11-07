-- Luồng sản xuất: yêu cầu QC -> kết quả QC -> báo cáo ca -> duyệt -> nhập kho.
-- Chạy sau BATT3.sql / BATT_data.sql trên database đang dùng. Không xóa dữ liệu cũ.
USE [ProductionManagementDB];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
IF OBJECT_ID(N'dbo.QCInspection', N'U') IS NULL OR OBJECT_ID(N'dbo.EndOfShiftReport', N'U') IS NULL
    THROW 50010, N'Chưa có database nền hoặc đang chọn sai database.', 1;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.ProductionQualityRequest', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductionQualityRequest (
        RequestId INT IDENTITY PRIMARY KEY,
        AssignmentId INT NOT NULL REFERENCES dbo.PersonnelAssignment(assignment_id),
        InspectionId INT NOT NULL UNIQUE REFERENCES dbo.QCInspection(inspection_id),
        ReportId INT NULL REFERENCES dbo.EndOfShiftReport(report_id),
        CreatedAt DATETIME2 NOT NULL
    );
    CREATE UNIQUE INDEX UX_ProductionQualityRequest_Report ON dbo.ProductionQualityRequest(ReportId) WHERE ReportId IS NOT NULL;
END;
IF OBJECT_ID(N'dbo.FinishedGoodsReceipt', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FinishedGoodsReceipt (
        ReceiptId INT IDENTITY PRIMARY KEY,
        QualityRequestId INT NOT NULL UNIQUE REFERENCES dbo.ProductionQualityRequest(RequestId),
        RequestedBy INT NULL REFERENCES dbo.[User](user_id),
        ReceivedBy INT NULL REFERENCES dbo.[User](user_id),
        RequestedAt DATETIME2 NOT NULL,
        ReceivedAt DATETIME2 NULL,
        ExpectedQuantity DECIMAL(12,2) NOT NULL CHECK (ExpectedQuantity > 0),
        ActualQuantity DECIMAL(12,2) NULL CHECK (ActualQuantity > 0),
        Status NVARCHAR(30) NOT NULL,
        RejectionReason NVARCHAR(1000) NULL,
        DiscrepancyNote NVARCHAR(2000) NULL,
        StaffConfirmedAt DATETIME2 NULL,
        CONSTRAINT CK_FinishedGoodsReceipt_Status CHECK (Status IN ('Pending','Rejected','Received','AwaitingStaffConfirmation'))
    );
END;
IF OBJECT_ID(N'dbo.ProductionMaterialRequest', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductionMaterialRequest (
        RequestId INT IDENTITY PRIMARY KEY,
        AssignmentId INT NOT NULL REFERENCES dbo.PersonnelAssignment(assignment_id),
        MaterialId INT NOT NULL REFERENCES dbo.RawMaterial(material_id),
        RequestedBy INT NULL REFERENCES dbo.[User](user_id),
        IssuedBy INT NULL REFERENCES dbo.[User](user_id),
        ExportId INT NULL REFERENCES dbo.RawMaterialExport(export_id),
        Quantity DECIMAL(12,2) NOT NULL CHECK (Quantity > 0),
        Status NVARCHAR(30) NOT NULL CHECK (Status IN ('Pending','Rejected','Issued')),
        Note NVARCHAR(2000) NULL,
        RejectionReason NVARCHAR(1000) NULL,
        RequestedAt DATETIME2 NOT NULL,
        IssuedAt DATETIME2 NULL
    );
END;
IF COL_LENGTH(N'dbo.FinishedGoodsReceipt', N'RequestNote') IS NULL
    ALTER TABLE dbo.FinishedGoodsReceipt ADD RequestNote NVARCHAR(2000) NULL;
COMMIT;
PRINT N'Đã bổ sung luồng QC và kho sản xuất; giữ nguyên dữ liệu hiện có.';
GO
