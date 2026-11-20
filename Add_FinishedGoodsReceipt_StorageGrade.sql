-- Lưu kho A/B do Thủ kho chọn riêng với kết quả phân loại của QC.
USE [ProductionManagementDB];
GO
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.FinishedGoodsReceipt', N'U') IS NULL
    THROW 50020, N'Không tìm thấy bảng FinishedGoodsReceipt. Hãy chọn đúng database dự án.', 1;

IF COL_LENGTH(N'dbo.FinishedGoodsReceipt', N'StorageGrade') IS NULL
    ALTER TABLE dbo.FinishedGoodsReceipt ADD StorageGrade NVARCHAR(1) NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_FinishedGoodsReceipt_StorageGrade'
      AND parent_object_id = OBJECT_ID(N'dbo.FinishedGoodsReceipt')
)
    ALTER TABLE dbo.FinishedGoodsReceipt
        ADD CONSTRAINT CK_FinishedGoodsReceipt_StorageGrade
        CHECK (StorageGrade IS NULL OR StorageGrade IN (N'A', N'B'));
GO

PRINT N'Đã thêm cột kho đích A/B cho phiếu nhập thành phẩm.';
GO
