-- Đồng bộ schema bảo trì với model hiện tại.
-- Chi them cot/bang con thieu; khong xoa, tao lai bang hoac sua noi dung du lieu cu.
USE [ProductionManagementDB];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.MaintenanceRequest', N'U') IS NULL
    THROW 50001, N'Không tìm thấy dbo.MaintenanceRequest. Kiểm tra database đang chọn.', 1;
IF OBJECT_ID(N'dbo.User', N'U') IS NULL
    THROW 50002, N'Không tìm thấy dbo.User. Kiểm tra database đang chọn.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.MaintenanceRequest', N'title') IS NULL
        ALTER TABLE dbo.MaintenanceRequest ADD title NVARCHAR(150) NULL;
    IF COL_LENGTH(N'dbo.MaintenanceRequest', N'location') IS NULL
        ALTER TABLE dbo.MaintenanceRequest ADD location NVARCHAR(200) NULL;
    IF COL_LENGTH(N'dbo.MaintenanceRequest', N'machine_stopped') IS NULL
        ALTER TABLE dbo.MaintenanceRequest ADD machine_stopped BIT NOT NULL
            CONSTRAINT DF_MaintenanceRequest_machine_stopped DEFAULT (0) WITH VALUES;
    IF COL_LENGTH(N'dbo.MaintenanceRequest', N'rejection_reason') IS NULL
        ALTER TABLE dbo.MaintenanceRequest ADD rejection_reason NVARCHAR(500) NULL;
    IF COL_LENGTH(N'dbo.MaintenanceRequest', N'due_date') IS NULL
        ALTER TABLE dbo.MaintenanceRequest ADD due_date DATE NULL;

    -- Cac cot cung duoc model bao tri hien tai su dung.
    IF COL_LENGTH(N'dbo.MaintenanceRequest', N'reviewed_by') IS NULL
        ALTER TABLE dbo.MaintenanceRequest ADD reviewed_by INT NULL;
    IF COL_LENGTH(N'dbo.MaintenanceRequest', N'reviewed_at') IS NULL
        ALTER TABLE dbo.MaintenanceRequest ADD reviewed_at DATETIME NULL;
    IF COL_LENGTH(N'dbo.MaintenanceRequest', N'assignment_note') IS NULL
        ALTER TABLE dbo.MaintenanceRequest ADD assignment_note NVARCHAR(500) NULL;
    IF COL_LENGTH(N'dbo.MaintenanceRequest', N'assigned_at') IS NULL
        ALTER TABLE dbo.MaintenanceRequest ADD assigned_at DATETIME NULL;
    IF COL_LENGTH(N'dbo.MaintenanceRequest', N'confirmed_at') IS NULL
        ALTER TABLE dbo.MaintenanceRequest ADD confirmed_at DATETIME NULL;
    IF COL_LENGTH(N'dbo.MaintenanceRequest', N'completed_at') IS NULL
        ALTER TABLE dbo.MaintenanceRequest ADD completed_at DATETIME NULL;
    IF COL_LENGTH(N'dbo.MaintenanceRequest', N'completion_note') IS NULL
        ALTER TABLE dbo.MaintenanceRequest ADD completion_note NVARCHAR(500) NULL;

    IF NOT EXISTS (
        SELECT 1 FROM sys.foreign_key_columns
        WHERE parent_object_id = OBJECT_ID(N'dbo.MaintenanceRequest')
          AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.MaintenanceRequest'), N'reviewed_by', 'ColumnId')
          AND referenced_object_id = OBJECT_ID(N'dbo.User')
    )
        EXEC sys.sp_executesql N'ALTER TABLE dbo.MaintenanceRequest WITH CHECK
            ADD CONSTRAINT FK_MR_reviewed_by FOREIGN KEY (reviewed_by) REFERENCES dbo.[User](user_id);';

    IF OBJECT_ID(N'dbo.MaintenanceRequestLog', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.MaintenanceRequestLog (
            log_id INT IDENTITY(1,1) CONSTRAINT PK_MaintenanceRequestLog PRIMARY KEY,
            request_id INT NOT NULL,
            action NVARCHAR(30) NOT NULL,
            from_status NVARCHAR(50) NULL,
            to_status NVARCHAR(50) NULL,
            actor_id INT NOT NULL,
            note NVARCHAR(500) NULL,
            created_at DATETIME NOT NULL,
            CONSTRAINT FK_MRL_Request FOREIGN KEY (request_id) REFERENCES dbo.MaintenanceRequest(request_id),
            CONSTRAINT FK_MRL_Actor FOREIGN KEY (actor_id) REFERENCES dbo.[User](user_id)
        );
    END;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MaintenanceRequestLog') AND name = N'IX_MRL_request_created')
        CREATE INDEX IX_MRL_request_created ON dbo.MaintenanceRequestLog(request_id, created_at DESC);

    COMMIT TRANSACTION;
    PRINT N'Đã cập nhật MaintenanceRequest và nhật ký bảo trì. Tiếp tục cập nhật vật tư và thông báo trong file này.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
