USE [ProductionManagementDB];
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF COL_LENGTH(N'dbo.ProductionQualityRequest', N'ResultImage') IS NULL
    ALTER TABLE dbo.ProductionQualityRequest ADD ResultImage VARBINARY(MAX) NULL;
IF COL_LENGTH(N'dbo.ProductionQualityRequest', N'ResultImageType') IS NULL
    ALTER TABLE dbo.ProductionQualityRequest ADD ResultImageType NVARCHAR(30) NULL;
COMMIT;
