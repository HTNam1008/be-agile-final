SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @OpenedAt DATETIMEOFFSET = TODATETIMEOFFSET(@Now, '+00:00');
DECLARE @ClosedAt DATETIMEOFFSET = TODATETIMEOFFSET(DATEADD(day, -1, @Now), '+00:00');
DECLARE @SchoolAOrgId BIGINT;
DECLARE @SchoolBOrgId BIGINT = 900102;
DECLARE @ActorLoginAccountId BIGINT = 1001;
DECLARE @FixturePersonIds TABLE (PersonId BIGINT PRIMARY KEY);
DECLARE @FixtureAccountIds TABLE (EducationAccountId BIGINT PRIMARY KEY);

SELECT @SchoolAOrgId = OrganizationId
FROM org.Organization
WHERE OrganizationCode = 'DEMO_SCHOOL';

IF @SchoolAOrgId IS NULL
BEGIN
    SELECT TOP (1) @SchoolAOrgId = OrganizationId
    FROM org.Organization
    WHERE OrganizationTypeCode = 'SCHOOL'
    ORDER BY OrganizationId;
END;

IF @SchoolAOrgId IS NULL
BEGIN
    RAISERROR('No existing SCHOOL organization found. Run migrations/baseline seed first.', 16, 1);
    RETURN;
END;

IF NOT EXISTS (SELECT 1 FROM iam.LoginAccount WHERE LoginAccountId = @ActorLoginAccountId)
BEGIN
    SELECT TOP (1) @ActorLoginAccountId = LoginAccountId
    FROM iam.LoginAccount
    WHERE PortalAccessCode = 'ADMIN'
    ORDER BY LoginAccountId;
END;

IF @ActorLoginAccountId IS NULL
BEGIN
    RAISERROR('No admin login account found for opened-by columns.', 16, 1);
    RETURN;
END;

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

DELETE al
FROM audit.AuditLog al
WHERE al.EntityTypeCode = 'EducationAccount'
  AND (
        al.EntityId IN (SELECT EducationAccountId FROM @FixtureAccountIds)
        OR EXISTS
        (
            SELECT 1
            FROM @FixturePersonIds f
            WHERE al.ChangedFieldsJson LIKE CONCAT('%"personId":', f.PersonId, '%')
        )
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

IF NOT EXISTS (SELECT 1 FROM org.Organization WHERE OrganizationCode = 'QA_TEST_AUTO002_SCHOOL_B')
BEGIN
    SET IDENTITY_INSERT org.Organization ON;
    INSERT INTO org.Organization
        (OrganizationId, ParentOrganizationId, OrganizationCode, OrganizationName, OrganizationTypeCode,
         OrganizationStatusCode, EffectiveFromUtc, EffectiveToUtc, CreatedAt, MockPassSchoolCode, UpdatedAt)
    VALUES
        (@SchoolBOrgId, 1, 'QA_TEST_AUTO002_SCHOOL_B', 'QA TEST AUTO002 School B', 'SCHOOL',
         'ACTIVE', '2026-01-01T00:00:00', NULL, @Now, 'QA_TEST_AUTO002_SCHOOL_B', @Now);
    SET IDENTITY_INSERT org.Organization OFF;
END
ELSE
BEGIN
    SELECT @SchoolBOrgId = OrganizationId
    FROM org.Organization
    WHERE OrganizationCode = 'QA_TEST_AUTO002_SCHOOL_B';
END;

SET IDENTITY_INSERT person.Person ON;
;WITH PersonSeed AS
(
    SELECT *
    FROM (VALUES
        (911101, 'QA_TEST_AUTO002_EXISTING_ACTIVE_25', 'QA TEST AUTO002 Existing Active Account Age 25', 'S911101A', '2001-06-24', 'CITIZEN', 'ACTIVE'),
        (911102, 'QA_TEST_AUTO002_ACTIVE_31_CLOSE', 'QA TEST AUTO002 Active Account Age 31 Close', 'S911102B', '1995-06-24', 'CITIZEN', 'ACTIVE'),
        (911103, 'QA_TEST_AUTO002_CLOSED_31_NOOP', 'QA TEST AUTO002 Closed Account Age 31 Noop', 'S911103C', '1995-06-24', 'CITIZEN', 'ACTIVE'),
        (911104, 'QA_TEST_AUTO002_DISABLED_22_SKIP', 'QA TEST AUTO002 Disabled Eligible Skip', 'S911104D', '2004-06-24', 'CITIZEN', 'DISABLED')
    ) v(PersonId, MockPassPersonId, FullName, IdentityNumberMasked, DateOfBirthText, ResidencyStatusCode, PersonStatusCode)
)
INSERT INTO person.Person
    (PersonId, MockPassPersonId, FullName, DateOfBirth, NationalityCode, ResidencyStatusCode,
     PersonStatusCode, CreatedAt, IdentityNumberMasked, OfficialAddress, OfficialEmail, OfficialMobile,
     PreferredAddress, PreferredEmail, PreferredMobile, SourceUpdatedAt, UpdatedAt)
SELECT
    PersonId, MockPassPersonId, FullName, CAST(DateOfBirthText AS date), 'SG', ResidencyStatusCode,
    PersonStatusCode, @Now, IdentityNumberMasked, CONCAT(PersonId, ' QA Test Auto002 Avenue'),
    CONCAT(LOWER(MockPassPersonId), '@student.example.test'), '+6591020000',
    CONCAT(PersonId, ' QA Test Auto002 Avenue'), CONCAT(LOWER(MockPassPersonId), '@student.example.test'),
    '+6591020000', @Now, @Now
FROM PersonSeed s
WHERE NOT EXISTS (SELECT 1 FROM person.Person p WHERE p.PersonId = s.PersonId);
SET IDENTITY_INSERT person.Person OFF;

UPDATE p
SET
    p.FullName = s.FullName,
    p.DateOfBirth = CAST(s.DateOfBirthText AS date),
    p.NationalityCode = 'SG',
    p.ResidencyStatusCode = s.ResidencyStatusCode,
    p.PersonStatusCode = s.PersonStatusCode,
    p.IdentityNumberMasked = s.IdentityNumberMasked,
    p.UpdatedAt = @Now
FROM person.Person p
JOIN
(
    SELECT *
    FROM (VALUES
        (911101, 'QA TEST AUTO002 Existing Active Account Age 25', 'S911101A', '2001-06-24', 'CITIZEN', 'ACTIVE'),
        (911102, 'QA TEST AUTO002 Active Account Age 31 Close', 'S911102B', '1995-06-24', 'CITIZEN', 'ACTIVE'),
        (911103, 'QA TEST AUTO002 Closed Account Age 31 Noop', 'S911103C', '1995-06-24', 'CITIZEN', 'ACTIVE'),
        (911104, 'QA TEST AUTO002 Disabled Eligible Skip', 'S911104D', '2004-06-24', 'CITIZEN', 'DISABLED')
    ) v(PersonId, FullName, IdentityNumberMasked, DateOfBirthText, ResidencyStatusCode, PersonStatusCode)
) s ON s.PersonId = p.PersonId;

SET IDENTITY_INSERT person.SchoolEnrollment ON;
;WITH EnrollmentSeed AS
(
    SELECT *
    FROM (VALUES
        (921101, 911101, 'QA-AUTO002-101', 'BACHELOR', 'QA-E1'),
        (921102, 911102, 'QA-AUTO002-102', 'BACHELOR', 'QA-E2'),
        (921103, 911103, 'QA-AUTO002-103', 'BACHELOR', 'QA-E3'),
        (921104, 911104, 'QA-AUTO002-104', 'BACHELOR', 'QA-E4')
    ) v(SchoolEnrollmentId, PersonId, StudentNumber, LevelCode, ClassCode)
)
INSERT INTO person.SchoolEnrollment
    (SchoolEnrollmentId, PersonId, OrganizationId, StudentNumber, AcademicYear, LevelCode, ClassCode,
     SchoolingStatusCode, StatusReasonCode, StartDate, EndDate, SourceCode, CreatedAt, UpdatedAt)
SELECT
    SchoolEnrollmentId, PersonId, @SchoolAOrgId, StudentNumber, '2026', LevelCode, ClassCode,
    'ACTIVE', NULL, '2026-01-02', NULL, 'QA_TEST_AUTO002', @Now, @Now
FROM EnrollmentSeed s
WHERE NOT EXISTS (SELECT 1 FROM person.SchoolEnrollment e WHERE e.SchoolEnrollmentId = s.SchoolEnrollmentId);
SET IDENTITY_INSERT person.SchoolEnrollment OFF;

SET IDENTITY_INSERT account.EducationAccount ON;
;WITH AccountSeed AS
(
    SELECT *
    FROM (VALUES
        (931101, 911101, 'QA-AUTO002-EA-101', 'ACTIVE', CAST(NULL AS datetimeoffset), NULL, NULL),
        (931102, 911102, 'QA-AUTO002-EA-102', 'ACTIVE', CAST(NULL AS datetimeoffset), NULL, NULL),
        (931103, 911103, 'QA-AUTO002-EA-103', 'CLOSED', @ClosedAt, 'AUTO_AGE_LIMIT', NULL)
    ) v(EducationAccountId, PersonId, AccountNumber, AccountStatusCode, ClosedAt, ClosingReasonCode, ClosedByLoginAccountId)
)
INSERT INTO account.EducationAccount
    (EducationAccountId, PersonId, AccountNumber, AccountStatusCode, OpenedAt, OpeningTypeCode,
     OpeningReason, OpenedByLoginAccountId, ClosedAt, ClosingReasonCode, ClosingReason,
     CurrentBalance, ClosedByLoginAccountId, ClosingTypeCode, ClosureExceptionApprovedByLoginAccountId,
     ClosureExceptionReason, ClosureExceptionUntil, PendingClosureAt)
SELECT
    EducationAccountId, PersonId, AccountNumber, AccountStatusCode, @OpenedAt, 'MANUAL',
    'QA_TEST_AUTO002 manual seed account', @ActorLoginAccountId, ClosedAt, ClosingReasonCode, NULL,
    0.00, ClosedByLoginAccountId, CASE WHEN AccountStatusCode = 'CLOSED' THEN 'AUTOMATIC' ELSE NULL END,
    NULL, NULL, NULL, NULL
FROM AccountSeed s
WHERE NOT EXISTS (SELECT 1 FROM account.EducationAccount a WHERE a.EducationAccountId = s.EducationAccountId);
SET IDENTITY_INSERT account.EducationAccount OFF;

UPDATE a
SET
    a.PersonId = s.PersonId,
    a.AccountNumber = s.AccountNumber,
    a.AccountStatusCode = s.AccountStatusCode,
    a.OpenedAt = @OpenedAt,
    a.OpeningTypeCode = 'MANUAL',
    a.OpeningReason = 'QA_TEST_AUTO002 manual seed account',
    a.OpenedByLoginAccountId = @ActorLoginAccountId,
    a.ClosedAt = s.ClosedAt,
    a.ClosingReasonCode = s.ClosingReasonCode,
    a.ClosingReason = NULL,
    a.CurrentBalance = 0.00,
    a.ClosedByLoginAccountId = s.ClosedByLoginAccountId,
    a.ClosingTypeCode = CASE WHEN s.AccountStatusCode = 'CLOSED' THEN 'AUTOMATIC' ELSE NULL END,
    a.ClosureExceptionApprovedByLoginAccountId = NULL,
    a.ClosureExceptionReason = NULL,
    a.ClosureExceptionUntil = NULL,
    a.PendingClosureAt = NULL
FROM account.EducationAccount a
JOIN
(
    SELECT *
    FROM (VALUES
        (931101, 911101, 'QA-AUTO002-EA-101', 'ACTIVE', CAST(NULL AS datetimeoffset), NULL, NULL),
        (931102, 911102, 'QA-AUTO002-EA-102', 'ACTIVE', CAST(NULL AS datetimeoffset), NULL, NULL),
        (931103, 911103, 'QA-AUTO002-EA-103', 'CLOSED', @ClosedAt, 'AUTO_AGE_LIMIT', NULL)
    ) v(EducationAccountId, PersonId, AccountNumber, AccountStatusCode, ClosedAt, ClosingReasonCode, ClosedByLoginAccountId)
) s ON s.EducationAccountId = a.EducationAccountId;

COMMIT TRANSACTION;

SELECT 'org.Organization' AS TableName, COUNT(*) AS CreatedOrPresentCount FROM org.Organization WHERE OrganizationCode = 'QA_TEST_AUTO002_SCHOOL_B'
UNION ALL SELECT 'person.Person', COUNT(*) FROM person.Person WHERE MockPassPersonId LIKE 'QA_TEST_AUTO002_%'
UNION ALL SELECT 'person.SchoolEnrollment', COUNT(*) FROM person.SchoolEnrollment WHERE SourceCode = 'QA_TEST_AUTO002'
UNION ALL SELECT 'account.EducationAccount', COUNT(*) FROM account.EducationAccount WHERE AccountNumber LIKE 'QA-AUTO002-EA-%';
