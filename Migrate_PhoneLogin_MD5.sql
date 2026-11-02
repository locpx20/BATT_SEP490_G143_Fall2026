-- =====================================================================
-- Migrate_PhoneLogin_MD5.sql
-- 1) Chuẩn hoá số điện thoại (định danh đăng nhập mới) về dạng 0xxxxxxxxx
-- 2) Báo cáo tài khoản thiếu / sai định dạng / trùng số điện thoại
-- 3) Tạo unique index cho phone
-- 4) Mã hoá mật khẩu cũ (plaintext) sang MD5
-- Chạy được nhiều lần (idempotent). NÊN SAO LƯU DB TRƯỚC KHI CHẠY.
-- =====================================================================
SET NOCOUNT ON;
GO

-- 1) Chuẩn hoá SĐT: bỏ khoảng trắng, '.', '-', '(', ')'; đổi +84 / 0084 thành 0
UPDATE [User]
SET phone = LTRIM(RTRIM(
        REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(phone, ' ', ''), '.', ''), '-', ''), '(', ''), ')', '')))
WHERE phone IS NOT NULL;

UPDATE [User] SET phone = '0' + SUBSTRING(phone, 4, 20) WHERE phone LIKE '+84%';
UPDATE [User] SET phone = '0' + SUBSTRING(phone, 5, 20) WHERE phone LIKE '0084%';
UPDATE [User] SET phone = NULL WHERE phone = '';
GO

-- 2) Báo cáo các tài khoản chưa đăng nhập được bằng SĐT
PRINT N'--- Tài khoản KHÔNG có SĐT hoặc SĐT sai định dạng (cần bổ sung trong trang Quản lý người dùng) ---';
SELECT user_id, username, full_name, phone
FROM [User]
WHERE phone IS NULL OR phone NOT LIKE '0%' OR phone LIKE '%[^0-9]%' OR LEN(phone) NOT IN (10, 11);

PRINT N'--- SĐT bị TRÙNG (phải sửa trước khi tạo unique index) ---';
SELECT phone, COUNT(*) AS so_tai_khoan, STRING_AGG(username, ', ') AS cac_tai_khoan
FROM [User]
WHERE phone IS NOT NULL
GROUP BY phone
HAVING COUNT(*) > 1;
GO

-- 3) Unique index cho phone (chỉ tạo khi không còn SĐT trùng)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_User_phone' AND object_id = OBJECT_ID(N'dbo.[User]'))
BEGIN
    IF EXISTS (SELECT phone FROM [User] WHERE phone IS NOT NULL GROUP BY phone HAVING COUNT(*) > 1)
        PRINT N'!! Bỏ qua tạo UX_User_phone: còn SĐT trùng. Sửa dữ liệu rồi chạy lại script này.';
    ELSE
        EXEC(N'CREATE UNIQUE INDEX UX_User_phone ON dbo.[User](phone) WHERE phone IS NOT NULL;');
END
GO

-- 4) Mã hoá mật khẩu plaintext -> MD5 (hex thường, 32 ký tự).
--    Cột password là VARCHAR nên HASHBYTES băm theo byte ASCII, khớp với MD5 trên UTF-8 của ứng dụng
--    (mật khẩu hệ thống chỉ cho phép ký tự ASCII). Bỏ qua các giá trị đã là MD5.
UPDATE [User]
SET password = LOWER(CONVERT(VARCHAR(32), HASHBYTES('MD5', password), 2))
WHERE NOT (LEN(password) = 32 AND password NOT LIKE '%[^0-9a-fA-F]%');

PRINT N'Hoàn tất: mật khẩu đã được mã hoá MD5. Đăng nhập bằng số điện thoại.';
GO
