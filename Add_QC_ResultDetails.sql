USE [ProductionManagementDB];
GO
SET XACT_ABORT ON;
IF OBJECT_ID(N'dbo.ProductionQualityRequest', N'U') IS NULL
    THROW 50010, N'Cần chạy Add_Production_QC_Warehouse_Workflow.sql trước.', 1;

IF OBJECT_ID(N'dbo.ProductionQcResult', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductionQcResult (
        RequestId INT NOT NULL PRIMARY KEY
            REFERENCES dbo.ProductionQualityRequest(RequestId) ON DELETE CASCADE,
        PelletColor NVARCHAR(100) NOT NULL,
        PelletDescription NVARCHAR(1000) NOT NULL,
        PelletLengthMm DECIMAL(8,2) NOT NULL,
        Compaction NVARCHAR(100) NOT NULL,
        AshPercent DECIMAL(5,2) NOT NULL,
        HasImpurities BIT NOT NULL,
        Grade NVARCHAR(1) NOT NULL,
        CONSTRAINT CK_ProductionQcResult_Length CHECK (PelletLengthMm > 0),
        CONSTRAINT CK_ProductionQcResult_Ash CHECK (AshPercent BETWEEN 0 AND 100),
        CONSTRAINT CK_ProductionQcResult_Grade CHECK (Grade IN (N'A', N'B'))
    );
END;
