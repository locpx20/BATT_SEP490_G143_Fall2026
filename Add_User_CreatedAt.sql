-- Preserve existing accounts. Their original creation dates are unknown.
-- Store new creation timestamps in UTC; the UI displays Vietnam time (UTC+7).
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.User', N'created_at') IS NULL
    EXEC(N'ALTER TABLE dbo.[User] ADD created_at datetime2(0) NULL;');

IF NOT EXISTS (
    SELECT 1 FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.User') AND c.name = N'created_at'
)
    EXEC(N'ALTER TABLE dbo.[User] ADD CONSTRAINT DF_User_CreatedAt DEFAULT SYSUTCDATETIME() FOR created_at;');

COMMIT TRANSACTION;
