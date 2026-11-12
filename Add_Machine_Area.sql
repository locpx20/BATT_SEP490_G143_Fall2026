-- Thêm khu vực đặt máy vào bảng Machine (không xóa / sửa dữ liệu cũ).
USE ProductionManagementDB;
GO

IF COL_LENGTH('dbo.Machine', 'area') IS NULL
BEGIN
    ALTER TABLE dbo.Machine ADD area NVARCHAR(200) NULL;
END
GO
