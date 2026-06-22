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
DECLARE @SchoolAOrgId BIGINT;
DECLARE @SchoolBOrgId BIGINT = 900002;
DECLARE @ActorLoginAccountId BIGINT = 1001;

SELECT TOP (1) @SchoolAOrgId = OrganizationId
FROM org.Organization
WHERE OrganizationTypeCode = 'SCHOOL'
  AND OrganizationCode <> 'QA_TEST_SCHOOL_B'
ORDER BY OrganizationId;

IF @SchoolAOrgId IS NULL
BEGIN
    RAISERROR('No existing SCHOOL organization found for School A. Run migrations/baseline seed first.', 16, 1);
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
    RAISERROR('No admin login account found for CreatedBy/OpenBy columns.', 16, 1);
    RETURN;
END;

BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM org.Organization WHERE OrganizationCode = 'QA_TEST_SCHOOL_B')
BEGIN
    SET IDENTITY_INSERT org.Organization ON;
    INSERT INTO org.Organization
        (OrganizationId, ParentOrganizationId, OrganizationCode, OrganizationName, OrganizationTypeCode,
         OrganizationStatusCode, EffectiveFromUtc, EffectiveToUtc, CreatedAt, MockPassSchoolCode, UpdatedAt)
    VALUES
        (@SchoolBOrgId, 1, 'QA_TEST_SCHOOL_B', 'QA TEST School B', 'SCHOOL',
         'ACTIVE', '2026-01-01T00:00:00', NULL, @Now, 'QA_TEST_SCHOOL_B', @Now);
    SET IDENTITY_INSERT org.Organization OFF;
END
ELSE
BEGIN
    SELECT @SchoolBOrgId = OrganizationId
    FROM org.Organization
    WHERE OrganizationCode = 'QA_TEST_SCHOOL_B';
END;

SET IDENTITY_INSERT person.Person ON;
;WITH PersonSeed AS
(
    SELECT *
    FROM (VALUES
        (910001, 'QA_TEST_PERSON_001', 'QA TEST Student 001 Active Account Courses', 'S910001A', 'CITIZEN'),
        (910002, 'QA_TEST_PERSON_002', 'QA TEST Student 002 Closed Account', 'S910002B', 'CITIZEN'),
        (910003, 'QA_TEST_PERSON_003', 'QA TEST Student 003 Closing Account', 'S910003C', 'VALID_PASS_HOLDER'),
        (910004, 'QA_TEST_PERSON_004', 'QA TEST Student 004', 'S910004D', 'CITIZEN'),
        (910005, 'QA_TEST_PERSON_005', 'QA TEST Student 005', 'S910005E', 'VALID_PASS_HOLDER'),
        (910006, 'QA_TEST_PERSON_006', 'QA TEST Student 006', 'S910006F', 'CITIZEN'),
        (910007, 'QA_TEST_PERSON_007', 'QA TEST Student 007', 'S910007G', 'VALID_PASS_HOLDER'),
        (910008, 'QA_TEST_PERSON_008', 'QA TEST Student 008', 'S910008H', 'CITIZEN'),
        (910009, 'QA_TEST_PERSON_009', 'QA TEST Student 009', 'S910009J', 'VALID_PASS_HOLDER'),
        (910010, 'QA_TEST_PERSON_010', 'QA TEST Student 010', 'S910010K', 'CITIZEN'),
        (910011, 'QA_TEST_PERSON_011', 'QA TEST Student 011', 'S910011L', 'VALID_PASS_HOLDER'),
        (910012, 'QA_TEST_PERSON_012', 'QA TEST Student 012', 'S910012M', 'CITIZEN'),
        (910013, 'QA_TEST_PERSON_013', 'QA TEST Student 013', 'S910013N', 'VALID_PASS_HOLDER'),
        (910014, 'QA_TEST_PERSON_014', 'QA TEST Student 014', 'S910014P', 'CITIZEN'),
        (910015, 'QA_TEST_PERSON_015', 'QA TEST Student 015', 'S910015Q', 'VALID_PASS_HOLDER'),
        (910016, 'QA_TEST_PERSON_016', 'QA TEST Student 016', 'S910016R', 'CITIZEN'),
        (910017, 'QA_TEST_PERSON_017', 'QA TEST Student 017', 'S910017T', 'VALID_PASS_HOLDER'),
        (910018, 'QA_TEST_PERSON_018', 'QA TEST Student 018', 'S910018U', 'CITIZEN'),
        (910019, 'QA_TEST_PERSON_019', 'QA TEST Student 019', 'S910019V', 'VALID_PASS_HOLDER'),
        (910020, 'QA_TEST_PERSON_020', 'QA TEST Student 020', 'S910020W', 'CITIZEN'),
        (910021, 'QA_TEST_PERSON_021', 'QA TEST Student 021', 'S910021X', 'VALID_PASS_HOLDER'),
        (910022, 'QA_TEST_PERSON_022', 'QA TEST Student 022', 'S910022Y', 'CITIZEN'),
        (910023, 'QA_TEST_PERSON_023', 'QA TEST Student 023', 'S910023Z', 'VALID_PASS_HOLDER'),
        (910024, 'QA_TEST_PERSON_024', 'QA TEST Student 024 No Account', 'S910024A', 'CITIZEN'),
        (910025, 'QA_TEST_PERSON_025', 'QA TEST Student 025 No Account', 'S910025B', 'VALID_PASS_HOLDER'),
        (910026, 'QA_TEST_PERSON_026', 'QA TEST Student 026 No Account', 'S910026C', 'CITIZEN'),
        (910027, 'QA_TEST_PERSON_027', 'QA TEST School B Student Active Account', 'S910027D', 'CITIZEN'),
        (910028, 'QA_TEST_PERSON_028', 'QA TEST School B Student', 'S910028E', 'VALID_PASS_HOLDER'),
        (910029, 'QA_TEST_PERSON_029', 'QA TEST School B Student', 'S910029F', 'CITIZEN'),
        (910030, 'QA_TEST_PERSON_030', 'QA TEST School B Student', 'S910030G', 'VALID_PASS_HOLDER'),
        (910031, 'QA_TEST_PERSON_031', 'QA TEST Account Holder No Enrollment', 'S910031H', 'CITIZEN')
    ) v(PersonId, MockPassPersonId, FullName, IdentityNumberMasked, ResidencyStatusCode)
)
INSERT INTO person.Person
    (PersonId, MockPassPersonId, FullName, DateOfBirth, NationalityCode, ResidencyStatusCode,
     PersonStatusCode, CreatedAt, IdentityNumberMasked, OfficialAddress, OfficialEmail, OfficialMobile,
     PreferredAddress, PreferredEmail, PreferredMobile, SourceUpdatedAt, UpdatedAt)
SELECT
    PersonId, MockPassPersonId, FullName, '2005-01-01', 'SG', ResidencyStatusCode,
    'ACTIVE', @Now, IdentityNumberMasked, CONCAT(PersonId, ' QA Test Avenue'),
    CONCAT(LOWER(MockPassPersonId), '@student.example.test'), '+6591990000',
    CONCAT(PersonId, ' QA Test Avenue'), CONCAT(LOWER(MockPassPersonId), '@student.example.test'),
    '+6591990000', @Now, @Now
FROM PersonSeed s
WHERE NOT EXISTS (SELECT 1 FROM person.Person p WHERE p.PersonId = s.PersonId);
SET IDENTITY_INSERT person.Person OFF;

SET IDENTITY_INSERT person.SchoolEnrollment ON;
;WITH EnrollmentSeed AS
(
    SELECT *
    FROM (VALUES
        (920001, 910001, 'QA-A-001', 'UNI_Y1', 'QA-A1', 'ACTIVE', NULL, NULL),
        (920002, 910002, 'QA-A-002', 'UNI_Y1', 'QA-A2', 'ACTIVE', NULL, NULL),
        (920003, 910003, 'QA-A-003', 'UNI_Y2', 'QA-B1', 'ACTIVE', NULL, NULL),
        (920004, 910004, 'QA-A-004', 'UNI_Y2', 'QA-B2', 'ACTIVE', NULL, NULL),
        (920005, 910005, 'QA-A-005', 'UNI_Y1', 'QA-A1', 'ACTIVE', NULL, NULL),
        (920006, 910006, 'QA-A-006', 'UNI_Y1', 'QA-A2', 'ACTIVE', NULL, NULL),
        (920007, 910007, 'QA-A-007', 'UNI_Y2', 'QA-B1', 'ACTIVE', NULL, NULL),
        (920008, 910008, 'QA-A-008', 'UNI_Y2', 'QA-B2', 'ACTIVE', NULL, NULL),
        (920009, 910009, 'QA-A-009', 'UNI_Y1', 'QA-A1', 'ACTIVE', NULL, NULL),
        (920010, 910010, 'QA-A-010', 'UNI_Y1', 'QA-A2', 'ACTIVE', NULL, NULL),
        (920011, 910011, 'QA-A-011', 'UNI_Y2', 'QA-B1', 'ACTIVE', NULL, NULL),
        (920012, 910012, 'QA-A-012', 'UNI_Y2', 'QA-B2', 'ACTIVE', NULL, NULL),
        (920013, 910013, 'QA-A-013', 'UNI_Y1', 'QA-A1', 'ACTIVE', NULL, NULL),
        (920014, 910014, 'QA-A-014', 'UNI_Y1', 'QA-A2', 'ACTIVE', NULL, NULL),
        (920015, 910015, 'QA-A-015', 'UNI_Y2', 'QA-B1', 'ACTIVE', NULL, NULL),
        (920016, 910016, 'QA-A-016', 'UNI_Y2', 'QA-B2', 'ACTIVE', NULL, NULL),
        (920017, 910017, 'QA-A-017', 'UNI_Y1', 'QA-A1', 'ACTIVE', NULL, NULL),
        (920018, 910018, 'QA-A-018', 'UNI_Y1', 'QA-A2', 'ACTIVE', NULL, NULL),
        (920019, 910019, 'QA-A-019', 'UNI_Y2', 'QA-B1', 'ACTIVE', NULL, NULL),
        (920020, 910020, 'QA-A-020', 'UNI_Y2', 'QA-B2', 'ACTIVE', NULL, NULL),
        (920021, 910021, 'QA-A-021', 'UNI_Y1', 'QA-A1', 'ACTIVE', NULL, NULL),
        (920022, 910022, 'QA-A-022', 'UNI_Y1', 'QA-A2', 'ACTIVE', NULL, NULL),
        (920023, 910023, 'QA-A-023', 'UNI_Y2', 'QA-B1', 'ACTIVE', NULL, NULL),
        (920024, 910024, 'QA-A-024', 'UNI_Y2', 'QA-B2', 'WITHDRAWN', 'QA_NOT_ENROLLED', '2025-12-31'),
        (920025, 910025, 'QA-A-025', 'UNI_Y1', 'QA-A1', 'GRADUATED', 'QA_NOT_ENROLLED', '2025-12-31'),
        (920026, 910026, 'QA-A-026', 'UNI_Y1', 'QA-A2', 'ON_LEAVE', 'QA_NOT_ENROLLED', '2025-12-31')
    ) v(SchoolEnrollmentId, PersonId, StudentNumber, LevelCode, ClassCode, SchoolingStatusCode, StatusReasonCode, EndDateText)
)
INSERT INTO person.SchoolEnrollment
    (SchoolEnrollmentId, PersonId, OrganizationId, StudentNumber, AcademicYear, LevelCode, ClassCode,
     SchoolingStatusCode, StatusReasonCode, StartDate, EndDate, SourceCode, CreatedAt, UpdatedAt)
SELECT
    SchoolEnrollmentId, PersonId, @SchoolAOrgId, StudentNumber, '2026', LevelCode, ClassCode,
    SchoolingStatusCode, StatusReasonCode, '2026-01-02', CAST(EndDateText AS date), 'QA_TEST', @Now, @Now
FROM EnrollmentSeed s
WHERE NOT EXISTS (SELECT 1 FROM person.SchoolEnrollment e WHERE e.SchoolEnrollmentId = s.SchoolEnrollmentId);

;WITH EnrollmentSeedB AS
(
    SELECT *
    FROM (VALUES
        (920027, 910027, 'QA-B-001', 'UNI_Y1', 'QA-C1', 'ACTIVE', NULL, NULL),
        (920028, 910028, 'QA-B-002', 'UNI_Y1', 'QA-C2', 'ACTIVE', NULL, NULL),
        (920029, 910029, 'QA-B-003', 'UNI_Y2', 'QA-D1', 'ACTIVE', NULL, NULL),
        (920030, 910030, 'QA-B-004', 'UNI_Y2', 'QA-D2', 'WITHDRAWN', 'QA_NOT_ENROLLED', '2025-12-31')
    ) v(SchoolEnrollmentId, PersonId, StudentNumber, LevelCode, ClassCode, SchoolingStatusCode, StatusReasonCode, EndDateText)
)
INSERT INTO person.SchoolEnrollment
    (SchoolEnrollmentId, PersonId, OrganizationId, StudentNumber, AcademicYear, LevelCode, ClassCode,
     SchoolingStatusCode, StatusReasonCode, StartDate, EndDate, SourceCode, CreatedAt, UpdatedAt)
SELECT
    SchoolEnrollmentId, PersonId, @SchoolBOrgId, StudentNumber, '2026', LevelCode, ClassCode,
    SchoolingStatusCode, StatusReasonCode, '2026-01-02', CAST(EndDateText AS date), 'QA_TEST', @Now, @Now
FROM EnrollmentSeedB s
WHERE NOT EXISTS (SELECT 1 FROM person.SchoolEnrollment e WHERE e.SchoolEnrollmentId = s.SchoolEnrollmentId);
SET IDENTITY_INSERT person.SchoolEnrollment OFF;

SET IDENTITY_INSERT account.EducationAccount ON;
;WITH AccountSeed AS
(
    SELECT *
    FROM (VALUES
        (930001, 910001, 'QA-EA-0001', 'ACTIVE',  CAST(NULL AS datetimeoffset), 250.00),
        (930002, 910002, 'QA-EA-0002', 'CLOSED',  @OpenedAt, 0.00),
        (930003, 910003, 'QA-EA-0003', 'CLOSING', CAST(NULL AS datetimeoffset), 0.00),
        (930004, 910027, 'QA-EA-0004', 'ACTIVE',  CAST(NULL AS datetimeoffset), 0.00),
        (930005, 910031, 'QA-EA-0005', 'ACTIVE',  CAST(NULL AS datetimeoffset), 0.00)
    ) v(EducationAccountId, PersonId, AccountNumber, AccountStatusCode, ClosedAt, CurrentBalance)
)
INSERT INTO account.EducationAccount
    (EducationAccountId, PersonId, AccountNumber, AccountStatusCode, OpenedAt, OpeningTypeCode,
     OpeningReason, OpenedByLoginAccountId, ClosedAt, ClosingReason, CurrentBalance,
     ClosedByLoginAccountId, ClosingTypeCode, ClosureExceptionApprovedByLoginAccountId,
     ClosureExceptionReason, ClosureExceptionUntil, PendingClosureAt)
SELECT
    EducationAccountId, PersonId, AccountNumber, AccountStatusCode, @OpenedAt, 'MANUAL',
    'QA_TEST manual seed account', @ActorLoginAccountId, ClosedAt, 'QA_TEST seeded close state',
    CurrentBalance, CASE WHEN AccountStatusCode = 'CLOSED' THEN @ActorLoginAccountId ELSE NULL END,
    CASE WHEN AccountStatusCode = 'CLOSED' THEN 'MANUAL' ELSE NULL END,
    NULL, NULL, NULL, CASE WHEN AccountStatusCode = 'CLOSING' THEN @OpenedAt ELSE NULL END
FROM AccountSeed s
WHERE NOT EXISTS (SELECT 1 FROM account.EducationAccount a WHERE a.EducationAccountId = s.EducationAccountId);
SET IDENTITY_INSERT account.EducationAccount OFF;

SET IDENTITY_INSERT account.AccountTransaction ON;
;WITH n AS
(
    SELECT TOP (25) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i
    FROM sys.objects
)
INSERT INTO account.AccountTransaction
    (AccountTransactionId, EducationAccountId, TransactionTypeCode, Amount, TransactionAt,
     ReferenceTypeCode, ReferenceId, IdempotencyKey, ReversalOfTransactionId,
     BalanceAfter, Description, CreatedByLoginAccountId)
SELECT
    940000 + i,
    930001,
    'CREDIT',
    10.00,
    DATEADD(day, i, '2026-06-01T08:00:00'),
    'TOPUP',
    NULL,
    CONCAT('QA_TEST_TOPUP_SYSTEM_', RIGHT(CONCAT('000', i), 3)),
    NULL,
    CAST(i * 10.00 AS decimal(19, 2)),
    CONCAT('QA_TEST system top-up ', i),
    NULL
FROM n
WHERE NOT EXISTS (
    SELECT 1
    FROM account.AccountTransaction t
    WHERE t.IdempotencyKey = CONCAT('QA_TEST_TOPUP_SYSTEM_', RIGHT(CONCAT('000', i), 3)));
SET IDENTITY_INSERT account.AccountTransaction OFF;

SET IDENTITY_INSERT course.Course ON;
;WITH CourseSeed AS
(
    SELECT *
    FROM (VALUES
        (950001, 'QA-COURSE-PENDING-NOBILL', 'QA TEST Pending No Bill Course'),
        (950002, 'QA-COURSE-PENDING-BILL', 'QA TEST Pending Billed Course'),
        (950003, 'QA-COURSE-COMPLETED', 'QA TEST Completed Course'),
        (950004, 'QA-COURSE-CANCELLED', 'QA TEST Cancelled Course'),
        (950005, 'QA-COURSE-EXITED', 'QA TEST Exited Course')
    ) v(CourseId, CourseCode, CourseName)
)
INSERT INTO course.Course
    (CourseId, OrganizationId, CourseCode, CourseName, Description, StartDate, EndDate,
     EnrollmentOpenAt, EnrollmentCloseAt, CourseStatusCode, CreatedByLoginAccountId,
     UpdatedByLoginAccountId, UpdatedAt, DisabledByLoginAccountId, DisabledAt)
SELECT
    CourseId, @SchoolAOrgId, CourseCode, CourseName, 'QA_TEST seeded course for ADM-33',
    '2026-07-01', '2026-12-31', '2026-06-01T00:00:00', '2026-06-30T23:59:59',
    'PUBLISHED', @ActorLoginAccountId, @ActorLoginAccountId, @Now, NULL, NULL
FROM CourseSeed s
WHERE NOT EXISTS (SELECT 1 FROM course.Course c WHERE c.CourseId = s.CourseId);
SET IDENTITY_INSERT course.Course OFF;

SET IDENTITY_INSERT course.CourseEnrollment ON;
;WITH CourseEnrollmentSeed AS
(
    SELECT *
    FROM (VALUES
        (960001, 910001, 950001, 'PENDING_PAYMENT', NULL, NULL),
        (960002, 910001, 950002, 'PENDING_PAYMENT', NULL, NULL),
        (960003, 910001, 950003, 'COMPLETED', NULL, NULL),
        (960004, 910001, 950004, 'CANCELLED', '2026-08-01T00:00:00', 'QA_CANCELLED'),
        (960005, 910001, 950005, 'EXITED', '2026-09-01T00:00:00', 'QA_EXITED')
    ) v(CourseEnrollmentId, PersonId, CourseId, EnrollmentStatusCode, ExitAtText, ExitReasonCode)
)
INSERT INTO course.CourseEnrollment
    (CourseEnrollmentId, PersonId, CourseId, EnrollmentSourceCode, EnrolledByLoginAccountId,
     EnrolledAt, EnrollmentStatusCode, ExitAt, ExitReasonCode)
SELECT
    CourseEnrollmentId, PersonId, CourseId, 'ADMIN_ADD', @ActorLoginAccountId,
    DATEADD(day, CourseEnrollmentId - 960000, '2026-06-01T08:00:00'), EnrollmentStatusCode,
    CAST(ExitAtText AS datetime2), ExitReasonCode
FROM CourseEnrollmentSeed s
WHERE NOT EXISTS (SELECT 1 FROM course.CourseEnrollment e WHERE e.CourseEnrollmentId = s.CourseEnrollmentId);
SET IDENTITY_INSERT course.CourseEnrollment OFF;

SET IDENTITY_INSERT billing.Bill ON;
IF NOT EXISTS (SELECT 1 FROM billing.Bill WHERE BillId = 970001)
BEGIN
    INSERT INTO billing.Bill
        (BillId, BillNumber, CourseEnrollmentId, IssuedAt, DueDate, GrossAmount, SubsidyAmount,
         NetPayableAmount, PaidAmount, OutstandingAmount, BillStatusCode)
    VALUES
        (970001, 'QA-BILL-0001', 960002, '2026-06-10T08:00:00', '2026-07-10',
         120.00, 20.00, 100.00, 0.00, 100.00, 'ISSUED');
END;
SET IDENTITY_INSERT billing.Bill OFF;

COMMIT TRANSACTION;

SELECT 'org.Organization' AS TableName, COUNT(*) AS CreatedOrPresentCount FROM org.Organization WHERE OrganizationCode = 'QA_TEST_SCHOOL_B'
UNION ALL SELECT 'person.Person', COUNT(*) FROM person.Person WHERE MockPassPersonId LIKE 'QA_TEST_PERSON_%'
UNION ALL SELECT 'person.SchoolEnrollment', COUNT(*) FROM person.SchoolEnrollment WHERE SourceCode = 'QA_TEST'
UNION ALL SELECT 'account.EducationAccount', COUNT(*) FROM account.EducationAccount WHERE AccountNumber LIKE 'QA-EA-%'
UNION ALL SELECT 'account.AccountTransaction', COUNT(*) FROM account.AccountTransaction WHERE IdempotencyKey LIKE 'QA_TEST_%'
UNION ALL SELECT 'course.Course', COUNT(*) FROM course.Course WHERE CourseCode LIKE 'QA-%'
UNION ALL SELECT 'course.CourseEnrollment', COUNT(*) FROM course.CourseEnrollment WHERE CourseEnrollmentId BETWEEN 960001 AND 960005
UNION ALL SELECT 'billing.Bill', COUNT(*) FROM billing.Bill WHERE BillNumber LIKE 'QA-BILL-%';
