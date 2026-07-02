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
    ('S7000061Z'),     ('S7000062H'),     ('S7000063F'),     ('S7000064D'),     ('S7000065B'),
    ('S7000066J'),     ('S7000067I'),     ('S7000068G'),     ('S7000069E'),     ('S7000070I'),
    ('S7000071G'),     ('S7000072E'),     ('S7000073C'),     ('S7000074A'),     ('S7000075Z'),
    ('S7000076H'),     ('S7000077F'),     ('S7000078D'),     ('S7000079B'),     ('S7000080F'),
    ('S7000081D'),     ('S7000082B'),     ('S7000083J'),     ('S7000084I'),     ('S7000085G'),
    ('S7000086E'),     ('S7000087C'),     ('S7000088A'),     ('S7000089Z'),     ('S7000090C'),
    ('S7000091A'),     ('S7000092Z'),     ('S7000093H'),     ('S7000094F'),     ('S7000095D'),
    ('S7000096B'),     ('S7000097J'),     ('S7000098I'),     ('S7000099G'),     ('S7000100D'),
    ('S7000101B'),     ('S7000102J'),     ('S7000103I'),     ('S7000104G'),     ('S7000105E'),
    ('S7000106C'),     ('S7000107A'),     ('S7000108Z'),     ('S7000109H'),     ('S7000110A'),
    ('S7000111Z'),     ('S7000112H'),     ('S7000113F'),     ('S7000114D'),     ('S7000115B'),
    ('S7000116J'),     ('S7000117I'),     ('S7000118G'),     ('S7000119E'),     ('S7000120I'),
    ('S7000121G'),     ('S7000122E'),     ('S7000123C'),     ('S7000124A'),     ('S7000125Z'),
    ('S7000126H'),     ('S7000127F'),     ('S7000128D'),     ('S7000129B'),     ('S7000130F'),
    ('S7000131D'),     ('S7000132B'),     ('S7000133J'),     ('S7000134I'),     ('S7000135G'),
    ('S7000136E'),     ('S7000137C'),     ('S7000138A'),     ('S7000139Z'),     ('S7000140C');

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

