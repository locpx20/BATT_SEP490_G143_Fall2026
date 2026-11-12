USE [ProductionManagementDB];
GO
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.ShiftProductionPlan', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ShiftProductionPlan (
        PlanDate DATE NOT NULL,
        Shift NVARCHAR(50) NOT NULL,
        ExpectedMaterialKg DECIMAL(12,2) NOT NULL,
        ExpectedFinishedKg DECIMAL(12,2) NOT NULL,
        Note NVARCHAR(1000) NULL,
        CONSTRAINT PK_ShiftProductionPlan PRIMARY KEY (PlanDate, Shift),
        CONSTRAINT CK_ShiftProductionPlan_Shift CHECK (Shift IN (N'Ca 1', N'Ca 2', N'Ca 3')),
        CONSTRAINT CK_ShiftProductionPlan_Quantity CHECK (ExpectedMaterialKg > 0 AND ExpectedFinishedKg > 0)
    );
END;
