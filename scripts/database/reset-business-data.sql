/*
    LOCAL/TEST DATABASE ONLY.

    Resets transactional/business data while preserving the seeded platform data:
      - people, student enrollments, login accounts, roles and permissions
      - organizations/schools
      - shared fee component catalogue
      - MFA credentials
      - education accounts (their balances and lifecycle state are reset)
      - EF migration history

    Deleted domains:
      course, billing, topup, payment, fas, communication, mail, audit and ai;
      transient identity provisioning requests, MFA challenges and MFA audit events.

    The script is intentionally blocked until @ConfirmReset is changed to 1.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ConfirmReset bit = 1;

IF @ConfirmReset <> 1
BEGIN
    THROW 51000, 'Reset cancelled. Set @ConfirmReset = 1 after verifying the target database.', 1;
END;

DECLARE @TargetDatabase sysname = DB_NAME();
PRINT CONCAT('Resetting business data in database: ', QUOTENAME(@TargetDatabase));

DECLARE @TargetTables TABLE
(
    ObjectId int NOT NULL PRIMARY KEY,
    SchemaName sysname NOT NULL,
    TableName sysname NOT NULL
);

INSERT INTO @TargetTables (ObjectId, SchemaName, TableName)
SELECT t.object_id, s.name, t.name
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE (s.name IN ('course', 'billing', 'topup', 'payment', 'fas', 'communication', 'mail', 'audit', 'ai')
       AND NOT (s.name = 'course' AND t.name = 'FeeComponent'))
   OR (s.name = 'account' AND t.name IN
      (
          'AccountHold',
          'AccountSettlement',
          'AccountTransaction',
          'SettlementPreference',
          'EducationAccountLifecycleRunItem',
          'EducationAccountLifecycleRun'
      ))
   OR (s.name = 'iam' AND t.name IN
      (
          'IdentityProvisioningRequest',
          'LoginMfaAuditEvent',
          'LoginMfaChallenge'
      ));

IF NOT EXISTS (SELECT 1 FROM @TargetTables)
BEGIN
    THROW 51001, 'No target business tables were found. Verify that this is the expected MOE database.', 1;
END;

DECLARE @InitiallyEnabledForeignKeys TABLE
(
    ObjectId int NOT NULL PRIMARY KEY,
    ParentObjectId int NOT NULL
);

INSERT INTO @InitiallyEnabledForeignKeys (ObjectId, ParentObjectId)
SELECT fk.object_id, fk.parent_object_id
FROM sys.foreign_keys fk
WHERE fk.is_disabled = 0;

DECLARE @Sql nvarchar(max) = N'';

/* Disable only constraints that were enabled when this script started. */
SELECT @Sql = STRING_AGG(
    CAST(N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)
        + N' NOCHECK CONSTRAINT ' + QUOTENAME(fk.name) + N';' AS nvarchar(max)),
    CHAR(10))
FROM sys.foreign_keys fk
JOIN @InitiallyEnabledForeignKeys enabled ON enabled.ObjectId = fk.object_id
JOIN sys.tables t ON t.object_id = fk.parent_object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id;

BEGIN TRY
    BEGIN TRANSACTION;

    IF NULLIF(@Sql, N'') IS NOT NULL
        EXEC sys.sp_executesql @Sql;

    SET @Sql = N'';
    SELECT @Sql = STRING_AGG(
        CAST(N'DELETE FROM ' + QUOTENAME(SchemaName) + N'.' + QUOTENAME(TableName) + N';'
            AS nvarchar(max)),
        CHAR(10))
    FROM @TargetTables;

    EXEC sys.sp_executesql @Sql;

    /* A freshly migrated demo account starts active with a zero balance. */
    IF OBJECT_ID(N'account.EducationAccount', N'U') IS NOT NULL
    BEGIN
        UPDATE account.EducationAccount
        SET CurrentBalance = 0,
            AccountStatusCode = 'ACTIVE',
            PendingClosureAt = NULL,
            ClosureExceptionUntil = NULL,
            ClosureExceptionReason = NULL,
            ClosureExceptionApprovedByLoginAccountId = NULL,
            ClosedAt = NULL,
            ClosingTypeCode = NULL,
            ClosingReasonCode = NULL,
            ClosingReason = NULL,
            ClosedByLoginAccountId = NULL;
    END;

    SET @Sql = N'';
    SELECT @Sql = STRING_AGG(
        CAST(N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)
            + N' WITH CHECK CHECK CONSTRAINT ' + QUOTENAME(fk.name) + N';' AS nvarchar(max)),
        CHAR(10))
    FROM sys.foreign_keys fk
    JOIN @InitiallyEnabledForeignKeys enabled ON enabled.ObjectId = fk.object_id
    JOIN sys.tables t ON t.object_id = fk.parent_object_id
    JOIN sys.schemas s ON s.schema_id = t.schema_id;

    IF NULLIF(@Sql, N'') IS NOT NULL
        EXEC sys.sp_executesql @Sql;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT SchemaName, TableName
FROM @TargetTables
ORDER BY SchemaName, TableName;

SELECT
    COUNT(*) AS PreservedEducationAccounts,
    SUM(CASE WHEN CurrentBalance = 0 THEN 1 ELSE 0 END) AS AccountsWithZeroBalance
FROM account.EducationAccount;

PRINT 'Business data reset completed successfully.';
