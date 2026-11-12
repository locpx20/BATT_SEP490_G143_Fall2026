USE [ProductionManagementDB];
GO
SET XACT_ABORT ON;
IF OBJECT_ID(N'dbo.DailyMaterialPlan',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DailyMaterialPlan (
        PlanDate DATE NOT NULL PRIMARY KEY,
        ExpectedKg DECIMAL(12,2) NOT NULL CHECK(ExpectedKg>0),
        UpdatedBy INT NULL REFERENCES dbo.[User](user_id),
        UpdatedAt DATETIME2 NOT NULL
    );
END;
