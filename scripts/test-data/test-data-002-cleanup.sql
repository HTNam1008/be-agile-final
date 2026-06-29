SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

DECLARE @FixturePersonIds TABLE (PersonId BIGINT PRIMARY KEY);
DECLARE @FixtureAccountIds TABLE (EducationAccountId BIGINT PRIMARY KEY);
DECLARE @FixtureRunIds TABLE (EducationAccountLifecycleRunId BIGINT PRIMARY KEY);

BEGIN TRANSACTION;

INSERT INTO @FixturePersonIds (PersonId)
SELECT PersonId
FROM person.Person
WHERE MockPassPersonId LIKE 'QA_TEST_AUTO002_%'
   OR FullName LIKE 'QA TEST AUTO002%';

INSERT INTO @FixturePersonIds (PersonId)
SELECT DISTINCT pi.PersonId
FROM person.PersonIdentifier pi
WHERE pi.IdentifierMasked IN
(
    'S912001A', 'S912002B', 'S912003C', 'S912004D', 'S912005E',
    'S912006F', 'S912008H', 'S912009J',
    'S7000051E', 'S7000052F', 'S7000053G', 'S7000054H', 'S7000055J',
    'S7000056K', 'S7000057L', 'S7000058M', 'S7000059N', 'S7000060P'
)
AND NOT EXISTS (SELECT 1 FROM @FixturePersonIds f WHERE f.PersonId = pi.PersonId);

INSERT INTO @FixtureAccountIds (EducationAccountId)
SELECT EducationAccountId
FROM account.EducationAccount
WHERE AccountNumber LIKE 'QA-AUTO002-EA-%'
   OR PersonId IN (SELECT PersonId FROM @FixturePersonIds);

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
    OR EXISTS
    (
        SELECT 1
        FROM @FixturePersonIds f
        WHERE al.ChangedFieldsJson LIKE CONCAT('%"personId":', f.PersonId, '%')
           OR al.ChangedFieldsJson LIKE CONCAT('%"PersonId":', f.PersonId, '%')
    );

DELETE FROM account.AccountTransaction
WHERE EducationAccountId IN (SELECT EducationAccountId FROM @FixtureAccountIds);

DELETE FROM account.EducationAccount
WHERE EducationAccountId IN (SELECT EducationAccountId FROM @FixtureAccountIds);

DELETE FROM person.SchoolEnrollment
WHERE SourceCode = 'QA_TEST_AUTO002'
   OR PersonId IN (SELECT PersonId FROM @FixturePersonIds);

DELETE FROM person.PersonIdentifier
WHERE PersonId IN (SELECT PersonId FROM @FixturePersonIds);

DELETE FROM person.Person
WHERE PersonId IN (SELECT PersonId FROM @FixturePersonIds);

DELETE FROM org.Organization
WHERE OrganizationCode = 'QA_TEST_AUTO002_SCHOOL_B';

COMMIT TRANSACTION;

SELECT 'org.Organization' AS TableName, COUNT(*) AS RemainingSeedRows
FROM org.Organization
WHERE OrganizationCode = 'QA_TEST_AUTO002_SCHOOL_B'
UNION ALL SELECT 'person.Person', COUNT(*) FROM person.Person WHERE MockPassPersonId LIKE 'QA_TEST_AUTO002_%' OR FullName LIKE 'QA TEST AUTO002%'
UNION ALL SELECT 'person.PersonIdentifier', COUNT(*) FROM person.PersonIdentifier WHERE IdentifierMasked IN ('S912001A', 'S912002B', 'S912003C', 'S912004D', 'S912005E', 'S912006F', 'S912008H', 'S912009J', 'S7000051E', 'S7000052F', 'S7000053G', 'S7000054H', 'S7000055J', 'S7000056K', 'S7000057L', 'S7000058M', 'S7000059N', 'S7000060P')
UNION ALL SELECT 'person.SchoolEnrollment', COUNT(*) FROM person.SchoolEnrollment WHERE SourceCode = 'QA_TEST_AUTO002'
UNION ALL SELECT 'account.EducationAccount', COUNT(*) FROM account.EducationAccount WHERE AccountNumber LIKE 'QA-AUTO002-EA-%'
UNION ALL SELECT 'account.EducationAccount fixture persons', COUNT(*) FROM account.EducationAccount WHERE PersonId IN (SELECT PersonId FROM @FixturePersonIds)
UNION ALL SELECT 'account.EducationAccountLifecycleRunItem fixture persons', COUNT(*) FROM account.EducationAccountLifecycleRunItem WHERE PersonId IN (SELECT PersonId FROM @FixturePersonIds);
