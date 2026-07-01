SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

DECLARE @FixtureIdentityNumbers TABLE (IdentifierMasked NVARCHAR(50) PRIMARY KEY);
DECLARE @FixturePersonIds TABLE (PersonId BIGINT PRIMARY KEY);
DECLARE @FixtureAccountIds TABLE (EducationAccountId BIGINT PRIMARY KEY);
DECLARE @FixtureLoginAccountIds TABLE (LoginAccountId BIGINT PRIMARY KEY);
DECLARE @FixtureRunIds TABLE (EducationAccountLifecycleRunId BIGINT PRIMARY KEY);

INSERT INTO @FixtureIdentityNumbers (IdentifierMasked)
VALUES
    ('S7000061Q'), ('S7000062R'), ('S7000063T'), ('S7000064U'), ('S7000065V'),
    ('S7000066W'), ('S7000067X'), ('S7000068Y'), ('S7000069Z'), ('S7000070A'),
    ('S7000071B'), ('S7000072C'), ('S7000073D'), ('S7000074E'), ('S7000075F'),
    ('S7000076G'), ('S7000077H'), ('S7000078J'), ('S7000079K'), ('S7000080L'),
    ('S7000081M'), ('S7000082N'), ('S7000083P'), ('S7000084Q'), ('S7000085R'),
    ('S7000086T'), ('S7000087U'), ('S7000088V'), ('S7000089W'), ('S7000090X'),
    ('S7000091Y'), ('S7000092Z'), ('S7000093A'), ('S7000094B'), ('S7000095C'),
    ('S7000096D'), ('S7000097E'), ('S7000098F'), ('S7000099G'), ('S7000100H'),
    ('S7000101J'), ('S7000102K'), ('S7000103L'), ('S7000104M'), ('S7000105N'),
    ('S7000106P'), ('S7000107Q'), ('S7000108R'), ('S7000109T'), ('S7000110U'),
    ('S7000111V'), ('S7000112W'), ('S7000113X'), ('S7000114Y'), ('S7000115Z'),
    ('S7000116A'), ('S7000117B'), ('S7000118C'), ('S7000119D'), ('S7000120E'),
    ('S7000121F'), ('S7000122G'), ('S7000123H'), ('S7000124J'), ('S7000125K'),
    ('S7000126L'), ('S7000127M'), ('S7000128N'), ('S7000129P'), ('S7000130Q'),
    ('S7000131R'), ('S7000132T'), ('S7000133U'), ('S7000134V'), ('S7000135W'),
    ('S7000136X'), ('S7000137Y'), ('S7000138Z'), ('S7000139A'), ('S7000140B');

BEGIN TRANSACTION;

INSERT INTO @FixturePersonIds (PersonId)
SELECT PersonId
FROM person.Person
WHERE FullName LIKE 'QA TEST DEMO003%'
   OR FullName LIKE 'QA TEST DEMO004%'
   OR FullName LIKE 'QA TEST DEMO005%'
   OR FullName LIKE 'QA TEST DEMO006%';

INSERT INTO @FixturePersonIds (PersonId)
SELECT DISTINCT pi.PersonId
FROM person.PersonIdentifier pi
JOIN @FixtureIdentityNumbers fixture ON fixture.IdentifierMasked = pi.IdentifierMasked
WHERE NOT EXISTS (SELECT 1 FROM @FixturePersonIds f WHERE f.PersonId = pi.PersonId);

INSERT INTO @FixtureAccountIds (EducationAccountId)
SELECT EducationAccountId
FROM account.EducationAccount
WHERE PersonId IN (SELECT PersonId FROM @FixturePersonIds);

INSERT INTO @FixtureLoginAccountIds (LoginAccountId)
SELECT LoginAccountId
FROM iam.LoginAccount
WHERE PersonId IN (SELECT PersonId FROM @FixturePersonIds);

INSERT INTO @FixtureRunIds (EducationAccountLifecycleRunId)
SELECT DISTINCT item.EducationAccountLifecycleRunId
FROM account.EducationAccountLifecycleRunItem item
WHERE item.PersonId IN (SELECT PersonId FROM @FixturePersonIds)
   OR item.EducationAccountId IN (SELECT EducationAccountId FROM @FixtureAccountIds);

DELETE item
FROM account.EducationAccountLifecycleRunItem item
WHERE item.PersonId IN (SELECT PersonId FROM @FixturePersonIds)
   OR item.EducationAccountId IN (SELECT EducationAccountId FROM @FixtureAccountIds);

DELETE run
FROM account.EducationAccountLifecycleRun run
WHERE run.EducationAccountLifecycleRunId IN (SELECT EducationAccountLifecycleRunId FROM @FixtureRunIds)
  AND NOT EXISTS
      (
          SELECT 1
          FROM account.EducationAccountLifecycleRunItem remaining
          WHERE remaining.EducationAccountLifecycleRunId = run.EducationAccountLifecycleRunId
      );

DELETE al
FROM audit.AuditLog al
WHERE
    (
        al.EntityTypeCode = 'EducationAccount'
        AND al.EntityId IN (SELECT EducationAccountId FROM @FixtureAccountIds)
    )
    OR
    (
        al.EntityTypeCode IN ('Person', 'Student', 'UserAccount', 'LoginAccount')
        AND al.EntityId IN
            (
                SELECT PersonId FROM @FixturePersonIds
                UNION
                SELECT LoginAccountId FROM @FixtureLoginAccountIds
            )
    )
    OR EXISTS
    (
        SELECT 1
        FROM @FixturePersonIds f
        WHERE al.ChangedFieldsJson LIKE CONCAT('%"personId":', f.PersonId, '%')
           OR al.ChangedFieldsJson LIKE CONCAT('%"PersonId":', f.PersonId, '%')
    );

DELETE FROM account.AccountTransaction
WHERE EducationAccountId IN (SELECT EducationAccountId FROM @FixtureAccountIds);

DELETE FROM account.SettlementPreference
WHERE EducationAccountId IN (SELECT EducationAccountId FROM @FixtureAccountIds);

DELETE FROM account.EducationAccount
WHERE EducationAccountId IN (SELECT EducationAccountId FROM @FixtureAccountIds);

DELETE FROM iam.UserAccessScope
WHERE UserAccountId IN (SELECT LoginAccountId FROM @FixtureLoginAccountIds);

DELETE FROM iam.LoginMfaAuditEvent
WHERE LoginAccountId IN (SELECT LoginAccountId FROM @FixtureLoginAccountIds)
   OR PerformedByAccountId IN (SELECT LoginAccountId FROM @FixtureLoginAccountIds);

DELETE FROM iam.LoginMfaChallenge
WHERE LoginAccountId IN (SELECT LoginAccountId FROM @FixtureLoginAccountIds);

DELETE FROM iam.LoginMfaCredential
WHERE LoginAccountId IN (SELECT LoginAccountId FROM @FixtureLoginAccountIds);

DELETE FROM iam.LoginAccount
WHERE LoginAccountId IN (SELECT LoginAccountId FROM @FixtureLoginAccountIds);

DELETE FROM person.SchoolEnrollment
WHERE PersonId IN (SELECT PersonId FROM @FixturePersonIds);

DELETE FROM person.PersonIdentifier
WHERE PersonId IN (SELECT PersonId FROM @FixturePersonIds);

DELETE FROM person.Person
WHERE PersonId IN (SELECT PersonId FROM @FixturePersonIds);

COMMIT TRANSACTION;

SELECT 'person.Person' AS TableName, COUNT(*) AS RemainingRows
FROM person.Person
WHERE FullName LIKE 'QA TEST DEMO003%'
   OR FullName LIKE 'QA TEST DEMO004%'
   OR FullName LIKE 'QA TEST DEMO005%'
   OR FullName LIKE 'QA TEST DEMO006%'
UNION ALL
SELECT 'person.PersonIdentifier', COUNT(*)
FROM person.PersonIdentifier pi
JOIN @FixtureIdentityNumbers fixture ON fixture.IdentifierMasked = pi.IdentifierMasked
UNION ALL
SELECT 'person.SchoolEnrollment', COUNT(*)
FROM person.SchoolEnrollment
WHERE PersonId IN (SELECT PersonId FROM @FixturePersonIds)
UNION ALL
SELECT 'account.EducationAccount', COUNT(*)
FROM account.EducationAccount
WHERE PersonId IN (SELECT PersonId FROM @FixturePersonIds)
UNION ALL
SELECT 'account.SettlementPreference', COUNT(*)
FROM account.SettlementPreference
WHERE EducationAccountId IN (SELECT EducationAccountId FROM @FixtureAccountIds)
UNION ALL
SELECT 'account.EducationAccountLifecycleRunItem', COUNT(*)
FROM account.EducationAccountLifecycleRunItem
WHERE PersonId IN (SELECT PersonId FROM @FixturePersonIds)
UNION ALL
SELECT 'iam.LoginAccount', COUNT(*)
FROM iam.LoginAccount
WHERE PersonId IN (SELECT PersonId FROM @FixturePersonIds)
UNION ALL
SELECT 'iam.UserAccessScope', COUNT(*)
FROM iam.UserAccessScope
WHERE UserAccountId IN (SELECT LoginAccountId FROM @FixtureLoginAccountIds)
UNION ALL
SELECT 'iam.LoginMfaAuditEvent', COUNT(*)
FROM iam.LoginMfaAuditEvent
WHERE LoginAccountId IN (SELECT LoginAccountId FROM @FixtureLoginAccountIds)
   OR PerformedByAccountId IN (SELECT LoginAccountId FROM @FixtureLoginAccountIds)
UNION ALL
SELECT 'iam.LoginMfaChallenge', COUNT(*)
FROM iam.LoginMfaChallenge
WHERE LoginAccountId IN (SELECT LoginAccountId FROM @FixtureLoginAccountIds)
UNION ALL
SELECT 'iam.LoginMfaCredential', COUNT(*)
FROM iam.LoginMfaCredential
WHERE LoginAccountId IN (SELECT LoginAccountId FROM @FixtureLoginAccountIds);
