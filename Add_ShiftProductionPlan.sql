USE [ProductionManagementDB];
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.ShiftProductionPlan', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ShiftProductionPlan (
        PlanDate DATE NOT NULL,
        Shift NVARCHAR(50) NOT NULL,
        IsDayOff BIT NOT NULL CONSTRAINT DF_ShiftProductionPlan_IsDayOff DEFAULT (0),
        ExpectedMaterialKg DECIMAL(12,2) NOT NULL,
        ExpectedFinishedKg DECIMAL(12,2) NOT NULL,
        Note NVARCHAR(1000) NULL,
        CONSTRAINT PK_ShiftProductionPlan PRIMARY KEY (PlanDate, Shift),
        CONSTRAINT CK_ShiftProductionPlan_Shift CHECK (Shift IN (N'Ca 1', N'Ca 2', N'Ca 3')),
        CONSTRAINT CK_ShiftProductionPlan_Quantity CHECK (
            (IsDayOff = 0 AND ExpectedMaterialKg > 0 AND ExpectedFinishedKg > 0)
            OR (IsDayOff = 1 AND ExpectedMaterialKg = 0 AND ExpectedFinishedKg = 0))
    );
END;
ELSE IF COL_LENGTH(N'dbo.ShiftProductionPlan', N'IsDayOff') IS NULL
BEGIN
    ALTER TABLE dbo.ShiftProductionPlan ADD IsDayOff BIT NOT NULL
        CONSTRAINT DF_ShiftProductionPlan_IsDayOff DEFAULT (0);
    IF OBJECT_ID(N'dbo.CK_ShiftProductionPlan_Quantity', N'C') IS NOT NULL
        ALTER TABLE dbo.ShiftProductionPlan DROP CONSTRAINT CK_ShiftProductionPlan_Quantity;
    EXEC(N'ALTER TABLE dbo.ShiftProductionPlan ADD CONSTRAINT CK_ShiftProductionPlan_Quantity CHECK (
        (IsDayOff = 0 AND ExpectedMaterialKg > 0 AND ExpectedFinishedKg > 0)
        OR (IsDayOff = 1 AND ExpectedMaterialKg = 0 AND ExpectedFinishedKg = 0));');
END;

COMMIT TRANSACTION;
