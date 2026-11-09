-- =====================================================================
-- Add_MaintenanceMaterialFlow.sql
-- Luồng mua vật tư trong bảo trì:
--   Nhân viên bảo trì tự kiểm tra vật tư. Đủ thì sửa tiếp; thiếu thì tạo yêu cầu mua
--   -> Manager mua xong -> thông báo -> nhân viên sửa tiếp.
-- Thêm:
--   1) MaterialPurchaseRequest : bổ sung cột đơn vị, ghi chú, kết quả mua
--   2) AppNotification         : thông báo nội bộ gửi tới người dùng
-- Trạng thái mới của MaintenanceRequest: 'WaitingMaterial' (không cần đổi schema).
-- Chạy được nhiều lần (idempotent). NÊN SAO LƯU DB TRƯỚC KHI CHẠY.
-- =====================================================================
SET NOCOUNT ON;
GO

IF COL_LENGTH(N'dbo.MaterialPurchaseRequest', N'unit') IS NULL
    ALTER TABLE dbo.MaterialPurchaseRequest ADD unit NVARCHAR(20) NULL;
GO
IF COL_LENGTH(N'dbo.MaterialPurchaseRequest', N'note') IS NULL
    ALTER TABLE dbo.MaterialPurchaseRequest ADD note NVARCHAR(500) NULL;          -- ghi chú của nhân viên bảo trì
GO
IF COL_LENGTH(N'dbo.MaterialPurchaseRequest', N'result_note') IS NULL
    ALTER TABLE dbo.MaterialPurchaseRequest ADD result_note NVARCHAR(500) NULL;   -- ghi chú mua hàng / lý do từ chối của Manager
GO
IF COL_LENGTH(N'dbo.MaterialPurchaseRequest', N'total_cost') IS NULL
    ALTER TABLE dbo.MaterialPurchaseRequest ADD total_cost DECIMAL(14, 2) NULL;
GO
IF COL_LENGTH(N'dbo.MaterialPurchaseRequest', N'handled_at') IS NULL
    ALTER TABLE dbo.MaterialPurchaseRequest ADD handled_at DATETIME NULL;         -- thời điểm Manager xử lý
GO

IF OBJECT_ID(N'dbo.AppNotification', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppNotification (
        notification_id INT IDENTITY(1,1) PRIMARY KEY,
        user_id         INT NOT NULL,
        title           NVARCHAR(150) NOT NULL,
        message         NVARCHAR(500) NOT NULL,
        url             NVARCHAR(200) NULL,
        is_read         BIT NOT NULL CONSTRAINT DF_AppNotification_read DEFAULT 0,
        created_at      DATETIME NOT NULL CONSTRAINT DF_AppNotification_created DEFAULT GETDATE(),
        CONSTRAINT FK_AppNotification_User FOREIGN KEY (user_id) REFERENCES dbo.[User](user_id)
    );
    CREATE INDEX IX_AppNotification_user_unread ON dbo.AppNotification(user_id, is_read, created_at DESC);
END
GO

PRINT N'Hoàn tất.';
GO
