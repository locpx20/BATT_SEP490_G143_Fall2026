USE [ProductionManagementDB];
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.MaterialUsageReview', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MaterialUsageReview (
        ReportId INT NOT NULL PRIMARY KEY REFERENCES dbo.EndOfShiftReport(report_id),
        Status NVARCHAR(20) NOT NULL CHECK (Status IN ('Pending','Rejected','Approved')),
        RejectionReason NVARCHAR(1000) NULL,
        ReviewedBy INT NULL REFERENCES dbo.[User](user_id),
        ReviewedAt DATETIME2 NULL
    );
END;
-- Preserve historical stock postings. Never deduct them again.
INSERT INTO dbo.MaterialUsageReview(ReportId, Status)
SELECT u.ReportId, CASE WHEN SUM(CASE WHEN u.ExportId IS NULL THEN 1 ELSE 0 END) = 0 THEN 'Approved' ELSE 'Pending' END
FROM dbo.ShiftMaterialUsage u
WHERE NOT EXISTS(SELECT 1 FROM dbo.MaterialUsageReview r WHERE r.ReportId = u.ReportId)
GROUP BY u.ReportId;
COMMIT;
