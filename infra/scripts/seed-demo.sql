-- Review and execute only against an explicitly approved Dev/Test database.
-- This script is not executed by application startup or the verification script.
SET XACT_ABORT ON;
IF DB_NAME() NOT IN (N'ShooterRecordsLabDev', N'ShooterRecordsLabTest')
    THROW 50002, 'Demo seed is restricted to the approved development/test database.', 1;

BEGIN TRANSACTION;
IF NOT EXISTS (SELECT 1 FROM dbo.Players WHERE Id = '11111111-1111-1111-1111-111111111111')
    INSERT INTO dbo.Players (Id, DisplayName, CreatedAtUtc)
    VALUES ('11111111-1111-1111-1111-111111111111', N'PracticePlayerA', '2026-10-20T08:00:00+00:00');
IF NOT EXISTS (SELECT 1 FROM dbo.Players WHERE Id = '22222222-2222-2222-2222-222222222222')
    INSERT INTO dbo.Players (Id, DisplayName, CreatedAtUtc)
    VALUES ('22222222-2222-2222-2222-222222222222', N'PracticePlayerB', '2026-10-20T08:00:00+00:00');
COMMIT;
