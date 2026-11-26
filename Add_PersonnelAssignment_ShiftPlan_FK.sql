-- Chạy trên database ứng dụng, sau Add_ShiftProductionPlan.sql.
-- Giữ nguyên các phân công cũ chưa có kế hoạch: hai cột liên kết sẽ để NULL.
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.PersonnelAssignment', N'U') IS NULL
       OR OBJECT_ID(N'dbo.ShiftProductionPlan', N'U') IS NULL
        THROW 51000, N'Cần có PersonnelAssignment và ShiftProductionPlan trước khi chạy script này.', 1;

    IF COL_LENGTH(N'dbo.PersonnelAssignment', N'shift_plan_date') IS NULL
        ALTER TABLE dbo.PersonnelAssignment ADD shift_plan_date DATE NULL;
    IF COL_LENGTH(N'dbo.PersonnelAssignment', N'shift_plan_shift') IS NULL
        ALTER TABLE dbo.PersonnelAssignment ADD shift_plan_shift NVARCHAR(50) NULL;

    -- Chỉ liên kết dữ liệu đã khớp ngày và ca; không tạo kế hoạch hoặc số liệu giả.
    EXEC(N'
        UPDATE a
        SET shift_plan_date = p.PlanDate, shift_plan_shift = p.Shift
        FROM dbo.PersonnelAssignment AS a
        INNER JOIN dbo.ShiftProductionPlan AS p
            ON p.PlanDate = a.assignment_date AND p.Shift = a.shift
        WHERE a.shift_plan_date IS NULL AND a.shift_plan_shift IS NULL;');

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints
                   WHERE parent_object_id = OBJECT_ID(N'dbo.PersonnelAssignment')
                     AND name = N'CK_PersonnelAssignment_ShiftPlanMatch')
        EXEC(N'ALTER TABLE dbo.PersonnelAssignment WITH CHECK
            ADD CONSTRAINT CK_PersonnelAssignment_ShiftPlanMatch CHECK (
                (shift_plan_date IS NULL AND shift_plan_shift IS NULL)
                OR (shift_plan_date IS NOT NULL AND shift_plan_shift IS NOT NULL
                    AND shift IS NOT NULL AND shift_plan_date = assignment_date AND shift_plan_shift = shift)
            );');

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
                   WHERE parent_object_id = OBJECT_ID(N'dbo.PersonnelAssignment')
                     AND name = N'FK_PersonnelAssignment_ShiftProductionPlan')
        EXEC(N'ALTER TABLE dbo.PersonnelAssignment WITH CHECK
            ADD CONSTRAINT FK_PersonnelAssignment_ShiftProductionPlan
            FOREIGN KEY (shift_plan_date, shift_plan_shift)
            REFERENCES dbo.ShiftProductionPlan (PlanDate, Shift)
            ON DELETE SET NULL;');

    IF NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE object_id = OBJECT_ID(N'dbo.PersonnelAssignment')
                     AND name = N'IX_PersonnelAssignment_ShiftPlan')
        EXEC(N'CREATE INDEX IX_PersonnelAssignment_ShiftPlan
            ON dbo.PersonnelAssignment (shift_plan_date, shift_plan_shift);');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
