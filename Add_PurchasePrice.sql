-- =====================================================================
-- Add_PurchasePrice.sql
-- Chức năng "Giá thu mua nguyên liệu":
--   1) RawMaterial.purchase_price      : giá thu mua hiện hành (NULL = chưa thiết lập)
--   2) RawMaterialPriceHistory         : lịch sử mọi lần thay đổi giá (chỉ thêm, không sửa/xoá)
-- Chạy được nhiều lần (idempotent). NÊN SAO LƯU DB TRƯỚC KHI CHẠY.
-- =====================================================================
USE [ProductionManagementDB];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    -- Không tiếp tục nếu chọn sai database, thiếu bảng gốc hoặc không có quyền đọc bảng.
    IF DB_NAME() <> N'ProductionManagementDB'
        THROW 50001, N'Bạn phải chạy script trong database ProductionManagementDB.', 1;
    IF OBJECT_ID(N'dbo.RawMaterial', N'U') IS NULL
        THROW 50002, N'Không tìm thấy dbo.RawMaterial trong ProductionManagementDB hoặc tài khoản không có quyền truy cập bảng.', 1;
    IF OBJECT_ID(N'dbo.User', N'U') IS NULL
        THROW 50003, N'Không tìm thấy dbo.User trong ProductionManagementDB hoặc tài khoản không có quyền truy cập bảng.', 1;

    BEGIN TRANSACTION;

    -- 1) Giá hiện hành trên RawMaterial
    IF COL_LENGTH(N'dbo.RawMaterial', N'purchase_price') IS NULL
        EXEC(N'ALTER TABLE dbo.RawMaterial ADD purchase_price DECIMAL(12, 2) NULL
               CONSTRAINT CK_RawMaterial_purchase_price CHECK (purchase_price IS NULL OR purchase_price > 0);');

    -- 2) Bảng lịch sử thay đổi giá
    IF OBJECT_ID(N'dbo.RawMaterialPriceHistory', N'U') IS NULL
    BEGIN
    CREATE TABLE dbo.RawMaterialPriceHistory (
        history_id  INT IDENTITY(1,1) PRIMARY KEY,
        material_id INT NOT NULL,
        old_price   DECIMAL(12, 2) NULL,          -- NULL = lần thiết lập giá đầu tiên
        new_price   DECIMAL(12, 2) NOT NULL,
        changed_by  INT NULL,
        changed_at  DATETIME NOT NULL CONSTRAINT DF_RMPH_changed_at DEFAULT GETDATE(),
        reason      NVARCHAR(500) NULL,
        CONSTRAINT FK_RMPH_Material FOREIGN KEY (material_id) REFERENCES dbo.RawMaterial(material_id),
        CONSTRAINT FK_RMPH_User     FOREIGN KEY (changed_by)  REFERENCES dbo.[User](user_id),
        CONSTRAINT CK_RMPH_price    CHECK (new_price > 0 AND (old_price IS NULL OR old_price > 0))
    );
    END

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.RawMaterialPriceHistory') AND name = N'IX_RMPH_material_changed'
    )
        CREATE INDEX IX_RMPH_material_changed ON dbo.RawMaterialPriceHistory(material_id, changed_at DESC);

    COMMIT TRANSACTION;
    PRINT N'Hoàn tất cập nhật ProductionManagementDB. Vào mục "Giá thu mua" (Owner/Manager) để thiết lập giá cho từng nguyên liệu.';
    PRINT N'Nguyên liệu chưa có giá sẽ chưa thể dùng để tạo đơn mua.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
