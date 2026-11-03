-- Cho phép xóa tài khoản, giữ nguyên dữ liệu nghiệp vụ và để trống liên kết người dùng.
-- Script chỉ đổi cột khóa ngoại thành nullable; không xóa tài khoản hay dữ liệu.
USE [ProductionManagementDB];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    IF DB_NAME() <> N'ProductionManagementDB' OR OBJECT_ID(N'dbo.User', N'U') IS NULL
        THROW 50010, N'Không tìm thấy bảng User trong ProductionManagementDB.', 1;

    BEGIN TRANSACTION;
    DECLARE @sql nvarchar(max) = N'';
    SELECT @sql += N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)
        + N' ALTER COLUMN ' + QUOTENAME(c.name) + N' INT NULL;' + CHAR(10)
    FROM sys.foreign_key_columns fk
    JOIN sys.tables t ON t.object_id = fk.parent_object_id
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    JOIN sys.columns c ON c.object_id = fk.parent_object_id AND c.column_id = fk.parent_column_id
    WHERE fk.referenced_object_id = OBJECT_ID(N'dbo.User') AND c.is_nullable = 0;
    EXEC sys.sp_executesql @sql;
    COMMIT TRANSACTION;
    PRINT N'Đã cập nhật liên kết tài khoản. Dữ liệu nghiệp vụ được giữ nguyên.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
