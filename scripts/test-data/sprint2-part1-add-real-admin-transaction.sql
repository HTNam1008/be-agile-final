SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

-- Helper lookup after Zenda creates the School Admin via the real API:
-- SELECT LoginAccountId, LoginEmailNormalized, DisplayNameSnapshot
-- FROM iam.LoginAccount
-- WHERE LoginEmailNormalized = UPPER('qa.school.a.admin@moe.local')
--    OR ContactEmail = 'qa.school.a.admin@moe.local'
--    OR ProviderEmail = 'qa.school.a.admin@moe.local';

DECLARE @SchoolAdminLoginAccountId BIGINT = -1; -- Fill this in manually before running.
DECLARE @SchoolAdminEmail NVARCHAR(320) = N'qa.school.a.admin@moe.local';

IF @SchoolAdminLoginAccountId <= 0
BEGIN
    RAISERROR('Set @SchoolAdminLoginAccountId to the real iam.LoginAccount.LoginAccountId before running this script.', 16, 1);
    RETURN;
END;

IF NOT EXISTS (SELECT 1 FROM iam.LoginAccount WHERE LoginAccountId = @SchoolAdminLoginAccountId)
BEGIN
    RAISERROR('@SchoolAdminLoginAccountId does not exist in iam.LoginAccount.', 16, 1);
    RETURN;
END;

IF NOT EXISTS (SELECT 1 FROM account.EducationAccount WHERE EducationAccountId = 930001)
BEGIN
    RAISERROR('Seed account 930001 does not exist. Run sprint2-part1-seed.sql first.', 16, 1);
    RETURN;
END;

BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM account.AccountTransaction WHERE IdempotencyKey = 'QA_TEST_TOPUP_REAL_ADMIN_001')
BEGIN
    SET IDENTITY_INSERT account.AccountTransaction ON;
    INSERT INTO account.AccountTransaction
        (AccountTransactionId, EducationAccountId, TransactionTypeCode, Amount, TransactionAt,
         ReferenceTypeCode, ReferenceId, IdempotencyKey, ReversalOfTransactionId,
         BalanceAfter, Description, CreatedByLoginAccountId)
    VALUES
        (940026, 930001, 'CREDIT', 5.00, '2026-07-01T08:00:00',
         'TOPUP', NULL, 'QA_TEST_TOPUP_REAL_ADMIN_001', NULL,
         255.00, 'QA_TEST top-up created by real school admin', @SchoolAdminLoginAccountId);
    SET IDENTITY_INSERT account.AccountTransaction OFF;

    UPDATE account.EducationAccount
    SET CurrentBalance = 255.00
    WHERE EducationAccountId = 930001
      AND CurrentBalance < 255.00;
END;

COMMIT TRANSACTION;

SELECT AccountTransactionId, EducationAccountId, Amount, BalanceAfter, CreatedByLoginAccountId
FROM account.AccountTransaction
WHERE IdempotencyKey = 'QA_TEST_TOPUP_REAL_ADMIN_001';
