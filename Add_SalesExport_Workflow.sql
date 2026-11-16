-- Luồng bán hàng & xuất kho thành phẩm:
--   Manager tạo yêu cầu bán (loại A/B + số bao, hệ thống phân bổ lô FIFO)
--   -> Owner duyệt (gán khách hàng + giá) hoặc từ chối (ghi lý do)
--   -> Manager tạo / cập nhật yêu cầu xuất kho (tài xế, biển số, ngày lấy hàng)
--   -> Thủ kho kiểm tra: trả lại Manager sửa, hoặc lập phiếu xuất kho
--   -> Hoàn thành phiếu xuất kho: trừ tồn kho lô, đơn hàng chuyển Completed.
-- Chạy sau BATT3.sql / BATT_data.sql / Add_Production_QC_Warehouse_Workflow.sql. Không xóa dữ liệu cũ, chạy lại nhiều lần an toàn.
-- Quy ước đơn vị: [Order].quantity = kg (1 bao = 800 kg), unit_price = VNĐ/kg, OrderDetail.quantity = kg.
USE [ProductionManagementDB];
GO
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
IF OBJECT_ID(N'dbo.[Order]', N'U') IS NULL OR OBJECT_ID(N'dbo.Delivery', N'U') IS NULL OR OBJECT_ID(N'dbo.FinishedGoodBatch', N'U') IS NULL
    THROW 50020, N'Chưa có database nền hoặc đang chọn sai database.', 1;
GO

-- 1. [Order]: khách hàng / giá do Owner cập nhật sau khi duyệt nên cho phép NULL lúc Manager tạo yêu cầu.
ALTER TABLE dbo.[Order] ALTER COLUMN customer_id INT NULL;
ALTER TABLE dbo.[Order] ALTER COLUMN inventory_id INT NULL;
ALTER TABLE dbo.[Order] ALTER COLUMN unit_price DECIMAL(12, 2) NULL;
GO
IF COL_LENGTH(N'dbo.[Order]', N'grade') IS NULL
    ALTER TABLE dbo.[Order] ADD grade NVARCHAR(1) NULL CONSTRAINT CK_Order_Grade CHECK (grade IN (N'A', N'B'));
IF COL_LENGTH(N'dbo.[Order]', N'approved_at') IS NULL
    ALTER TABLE dbo.[Order] ADD approved_at DATETIME NULL;
IF COL_LENGTH(N'dbo.[Order]', N'note') IS NULL
    ALTER TABLE dbo.[Order] ADD note NVARCHAR(500) NULL;
GO

-- confirmed_by chỉ có giá trị khi thủ kho hoàn thành phiếu xuất; một số database cũ đang để NOT NULL nên phải cho phép NULL.
ALTER TABLE dbo.Delivery ALTER COLUMN confirmed_by INT NULL;
GO

-- 2. Delivery = yêu cầu xuất kho: thêm SĐT tài xế, ghi chú, lý do thủ kho trả lại, người tạo.
IF COL_LENGTH(N'dbo.Delivery', N'driver_phone') IS NULL
    ALTER TABLE dbo.Delivery ADD driver_phone NVARCHAR(20) NULL;
IF COL_LENGTH(N'dbo.Delivery', N'note') IS NULL
    ALTER TABLE dbo.Delivery ADD note NVARCHAR(500) NULL;
IF COL_LENGTH(N'dbo.Delivery', N'return_reason') IS NULL
    ALTER TABLE dbo.Delivery ADD return_reason NVARCHAR(500) NULL;
IF COL_LENGTH(N'dbo.Delivery', N'created_by') IS NULL
    ALTER TABLE dbo.Delivery ADD created_by INT NULL CONSTRAINT FK_Delivery_CreatedBy REFERENCES dbo.[User](user_id);
IF COL_LENGTH(N'dbo.Delivery', N'created_at') IS NULL
    ALTER TABLE dbo.Delivery ADD created_at DATETIME NULL;
IF COL_LENGTH(N'dbo.Delivery', N'updated_at') IS NULL
    ALTER TABLE dbo.Delivery ADD updated_at DATETIME NULL;
GO
-- Trạng thái Delivery: Prepared (chờ thủ kho kiểm tra) | Returned (thủ kho trả lại Manager sửa) | Delivered (đã xuất kho) | Cancelled.

-- 3. Phiếu xuất kho + chi tiết theo lô.
IF OBJECT_ID(N'dbo.ExportSlip', N'U') IS NULL
CREATE TABLE dbo.ExportSlip (
    slip_id INT IDENTITY(1,1) PRIMARY KEY,
    slip_code NVARCHAR(50) NOT NULL,
    delivery_id INT NOT NULL REFERENCES dbo.Delivery(delivery_id),
    order_id INT NOT NULL REFERENCES dbo.[Order](order_id),
    status NVARCHAR(30) NOT NULL CONSTRAINT DF_ExportSlip_Status DEFAULT N'Processing',
    note NVARCHAR(500) NULL,
    image_path NVARCHAR(300) NULL,
    created_by INT NULL REFERENCES dbo.[User](user_id),
    created_at DATETIME NOT NULL,
    completed_by INT NULL REFERENCES dbo.[User](user_id),
    completed_at DATETIME NULL,
    CONSTRAINT UQ_ExportSlip_Code UNIQUE (slip_code),
    CONSTRAINT UQ_ExportSlip_Delivery UNIQUE (delivery_id),
    CONSTRAINT CK_ExportSlip_Status CHECK (status IN (N'Processing', N'Completed'))
);
GO
-- Ảnh phiếu xuất kho thực tế (thủ kho chụp/tải lên khi tạo phiếu); bổ sung cho database đã có bảng ExportSlip.
IF COL_LENGTH(N'dbo.ExportSlip', N'image_path') IS NULL
    ALTER TABLE dbo.ExportSlip ADD image_path NVARCHAR(300) NULL;
GO
IF OBJECT_ID(N'dbo.ExportSlipLine', N'U') IS NULL
CREATE TABLE dbo.ExportSlipLine (
    line_id INT IDENTITY(1,1) PRIMARY KEY,
    slip_id INT NOT NULL REFERENCES dbo.ExportSlip(slip_id),
    batch_id INT NOT NULL REFERENCES dbo.FinishedGoodBatch(batch_id),
    grade NVARCHAR(1) NULL,
    bags INT NOT NULL CHECK (bags > 0),
    quantity DECIMAL(12, 2) NOT NULL CHECK (quantity > 0)
);
GO
PRINT N'Đã bổ sung luồng bán hàng và xuất kho; giữ nguyên dữ liệu hiện có.';
GO
