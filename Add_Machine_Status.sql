USE ProductionManagementDB;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('dbo.Machine') AND name = 'status'
)
BEGIN
    ALTER TABLE dbo.Machine
    ADD status NVARCHAR(50) NOT NULL CONSTRAINT DF_Machine_Status DEFAULT 'Active';
END
GO

