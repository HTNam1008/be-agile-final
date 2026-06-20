/*
    Canonical local-development seed data.

    Goals:
      - every developer gets the same campaign and run IDs;
      - the script is idempotent and safe to rerun;
      - B-003 run-summary states are represented consistently;
      - unrelated application data is preserved.

    Prerequisites:
      1. Apply the repository EF Core migrations.
      2. Ensure the standard identity seed exists (organizations 1/2 and admins 1001/1002).
      3. If the local database is schema-drifted, run repair-b003-topup-run-columns.sql first.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;

BEGIN TRANSACTION;

DECLARE @HqOrganizationId bigint = 1;
DECLARE @SchoolOrganizationId bigint = 2;
DECLARE @HqAdminId bigint = 1001;
DECLARE @SchoolAdminId bigint = 1002;

DECLARE @HqCampaignId bigint = 95001;
DECLARE @SchoolCampaignId bigint = 95002;

DECLARE @CompletedRunId bigint = 96001;
DECLARE @PartialRunId bigint = 96002;
DECLARE @ProcessingRunId bigint = 96003;
DECLARE @FailedRunId bigint = 96004;

DECLARE @CourseIdOne bigint = 92001;
DECLARE @CourseIdTwo bigint = 92002;
DECLARE @FeeComponentTuitionId bigint = 93001;
DECLARE @FeeComponentMaterialId bigint = 93002;
DECLARE @CourseFeeOneTuitionId bigint = 93101;
DECLARE @CourseFeeOneMaterialId bigint = 93102;
DECLARE @CourseFeeTwoTuitionId bigint = 93103;
DECLARE @CourseEnrollmentOneId bigint = 94001;
DECLARE @CourseEnrollmentTwoId bigint = 94002;
DECLARE @BillOneId bigint = 94501;
DECLARE @BillTwoId bigint = 94502;
DECLARE @BillLineOneId bigint = 94601;
DECLARE @BillLineTwoId bigint = 94602;
DECLARE @BillLineThreeId bigint = 94603;
DECLARE @FasSchemeId bigint = 98001;
DECLARE @FasTierId bigint = 98101;
DECLARE @FasTierBenefitId bigint = 98201;
DECLARE @CourseFasSchemeId bigint = 98301;
DECLARE @FasApplicationId bigint = 98401;
DECLARE @FasSubsidyId bigint = 98501;
DECLARE @PaymentId bigint = 99001;
DECLARE @PaymentPartId bigint = 99101;

DECLARE @DemoStudents TABLE
(
    PersonId bigint PRIMARY KEY,
    SchoolEnrollmentId bigint NOT NULL,
    EducationAccountId bigint NOT NULL,
    OrganizationId bigint NOT NULL,
    StudentNumber nvarchar(50) NOT NULL,
    FullName nvarchar(200) NOT NULL,
    DateOfBirth date NOT NULL,
    LevelCode varchar(30) NOT NULL,
    ClassCode varchar(30) NOT NULL,
    SchoolingStatusCode varchar(30) NOT NULL,
    AccountNumber nvarchar(50) NOT NULL,
    AccountStatusCode varchar(30) NOT NULL,
    CurrentBalance decimal(19, 2) NOT NULL
);

INSERT INTO @DemoStudents
(
    PersonId,
    SchoolEnrollmentId,
    EducationAccountId,
    OrganizationId,
    StudentNumber,
    FullName,
    DateOfBirth,
    LevelCode,
    ClassCode,
    SchoolingStatusCode,
    AccountNumber,
    AccountStatusCode,
    CurrentBalance
)
VALUES
(21001, 31001, 41001, @SchoolOrganizationId, N'DEMO-STU-1001', N'Alya Rahman', '2010-02-14', 'SEC_3', '3A', 'ACTIVE', N'EA-DEMO-1001', 'ACTIVE', 12.50),
(21002, 31002, 41002, @SchoolOrganizationId, N'DEMO-STU-1002', N'Brandon Lim', '2009-07-03', 'SEC_4', '4B', 'ACTIVE', N'EA-DEMO-1002', 'ACTIVE', 245.00),
(21003, 31003, 41003, @SchoolOrganizationId, N'DEMO-STU-1003', N'Chen Wei Jie', '2011-11-21', 'SEC_2', '2C', 'ON_LEAVE', N'EA-DEMO-1003', 'ACTIVE', 4.75),
(21004, 31004, 41004, @SchoolOrganizationId, N'DEMO-STU-1004', N'Diya Nair', '2012-04-09', 'SEC_1', '1A', 'ACTIVE', N'EA-DEMO-1004', 'ACTIVE', 0.00),
(21005, 31005, 41005, @SchoolOrganizationId, N'DEMO-STU-1005', N'Ethan Wong', '2008-12-30', 'SEC_5', '5A', 'ACTIVE', N'EA-DEMO-1005', 'ACTIVE', 502.20),
(21006, 31006, 41006, @SchoolOrganizationId, N'DEMO-STU-1006', N'Farah Tan', '2010-09-18', 'SEC_3', '3B', 'ON_LEAVE', N'EA-DEMO-1006', 'ACTIVE', 89.90),
(21007, 31007, 41007, @HqOrganizationId, N'DEMO-STU-1007', N'Gabriel Koh', '2009-01-05', 'SEC_4', '4A', 'ACTIVE', N'EA-DEMO-1007', 'ACTIVE', 33.30),
(21008, 31008, 41008, @HqOrganizationId, N'DEMO-STU-1008', N'Hana Sato', '2011-06-27', 'SEC_2', '2B', 'ACTIVE', N'EA-DEMO-1008', 'ACTIVE', 151.15),
(21009, 31009, 41009, @HqOrganizationId, N'DEMO-STU-1009', N'Irfan Ismail', '2013-03-12', 'PRI_6', '6D', 'ACTIVE', N'EA-DEMO-1009', 'ACTIVE', 18.00),
(21010, 31010, 41010, @SchoolOrganizationId, N'DEMO-STU-1010', N'Janelle Lee', '2012-10-02', 'SEC_1', '1C', 'ACTIVE', N'EA-DEMO-1010', 'ACTIVE', 75.45),
(21011, 31011, 41011, @SchoolOrganizationId, N'DEMO-STU-1011', N'Kai Anwar', '2010-05-24', 'SEC_3', '3C', 'ACTIVE', N'EA-DEMO-1011', 'ACTIVE', 310.00),
(21012, 31012, 41012, @SchoolOrganizationId, N'DEMO-STU-1012', N'Leah Pereira', '2009-08-16', 'SEC_4', '4C', 'ACTIVE', N'EA-DEMO-1012', 'ACTIVE', 42.00);

IF OBJECT_ID(N'topup.TopUpCampaign', N'U') IS NULL
    THROW 50001, 'Table topup.TopUpCampaign is missing. Apply migrations first.', 1;

IF OBJECT_ID(N'topup.TopUpRun', N'U') IS NULL
    THROW 50002, 'Table topup.TopUpRun is missing. Apply migrations first.', 1;

IF COL_LENGTH(N'topup.TopUpRun', N'Note') IS NULL
    THROW 50003, 'Column topup.TopUpRun.Note is missing. Run repair-b003-topup-run-columns.sql.', 1;

IF COL_LENGTH(N'topup.TopUpRun', N'TotalSkipped') IS NULL
    THROW 50004, 'Column topup.TopUpRun.TotalSkipped is missing. Run repair-b003-topup-run-columns.sql.', 1;

IF OBJECT_ID(N'person.Person', N'U') IS NULL
    THROW 50011, 'Table person.Person is missing. Apply migrations first.', 1;

IF OBJECT_ID(N'person.SchoolEnrollment', N'U') IS NULL
    THROW 50012, 'Table person.SchoolEnrollment is missing. Apply migrations first.', 1;

IF OBJECT_ID(N'account.EducationAccount', N'U') IS NULL
    THROW 50013, 'Table account.EducationAccount is missing. Apply migrations first.', 1;

IF OBJECT_ID(N'course.Course', N'U') IS NULL
    THROW 50016, 'Table course.Course is missing. Apply migrations first.', 1;

IF OBJECT_ID(N'billing.Bill', N'U') IS NULL
    THROW 50017, 'Table billing.Bill is missing. Apply migrations first.', 1;

IF OBJECT_ID(N'fas.FASScheme', N'U') IS NULL
    THROW 50018, 'Table fas.FASScheme is missing. Apply migrations first.', 1;

IF OBJECT_ID(N'payment.Payment', N'U') IS NULL
    THROW 50019, 'Table payment.Payment is missing. Apply migrations first.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM org.Organization
    WHERE OrganizationId = @HqOrganizationId
)
    THROW 50005, 'Seed organization 1 is missing. Apply the standard migrations first.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM org.Organization
    WHERE OrganizationId = @SchoolOrganizationId
)
    THROW 50006, 'Seed organization 2 is missing. Apply the standard migrations first.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM iam.LoginAccount
    WHERE LoginAccountId = @HqAdminId
)
    THROW 50007, 'Seed HQ admin 1001 is missing. Apply the standard migrations first.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM iam.LoginAccount
    WHERE LoginAccountId = @SchoolAdminId
)
    THROW 50008, 'Seed school admin 1002 is missing. Apply the standard migrations first.', 1;

IF EXISTS (
    SELECT 1
    FROM topup.TopUpCampaign
    WHERE TopUpCampaignId IN (@HqCampaignId, @SchoolCampaignId)
      AND CampaignCode NOT IN (N'LOCAL-HQ-TOPUP', N'LOCAL-SCHOOL-TOPUP')
)
    THROW 50009, 'Reserved local campaign IDs 95001/95002 are used by unrelated data.', 1;

IF EXISTS (
    SELECT 1
    FROM topup.TopUpRun
    WHERE TopUpRunId IN (@CompletedRunId, @PartialRunId, @ProcessingRunId, @FailedRunId)
      AND IdempotencyKey NOT LIKE N'LOCAL-RUN-%'
)
    THROW 50010, 'Reserved local run IDs 96001-96004 are used by unrelated data.', 1;

IF EXISTS (
    SELECT 1
    FROM person.Person AS person
    INNER JOIN @DemoStudents AS demo
        ON demo.PersonId = person.PersonId
    WHERE person.MockPassPersonId NOT LIKE N'LOCAL-TOPUP-STUDENT-%'
)
    THROW 50014, 'Reserved local person IDs 21001-21012 are used by unrelated data.', 1;

IF EXISTS (
    SELECT 1
    FROM account.EducationAccount AS account
    INNER JOIN @DemoStudents AS demo
        ON demo.EducationAccountId = account.EducationAccountId
    WHERE account.AccountNumber NOT LIKE N'EA-DEMO-10%'
)
    THROW 50015, 'Reserved local education account IDs 41001-41012 are used by unrelated data.', 1;

UPDATE personRow
SET
    MockPassPersonId = CONCAT(N'LOCAL-TOPUP-STUDENT-', demo.StudentNumber),
    IdentityNumberMasked = CONCAT(N'S****', RIGHT(demo.StudentNumber, 4)),
    FullName = demo.FullName,
    DateOfBirth = demo.DateOfBirth,
    NationalityCode = 'SG',
    ResidencyStatusCode = 'CITIZEN',
    OfficialEmail = CONCAT(LOWER(REPLACE(demo.StudentNumber, N'-', N'')), N'@example.test'),
    PreferredEmail = CONCAT(LOWER(REPLACE(demo.StudentNumber, N'-', N'')), N'@example.test'),
    OfficialMobile = '+6590000000',
    PreferredMobile = '+6590000000',
    OfficialAddress = 'Demo address, Singapore',
    PreferredAddress = 'Demo address, Singapore',
    PersonStatusCode = 'ACTIVE',
    SourceUpdatedAt = '2026-06-01T00:00:00',
    UpdatedAt = '2026-06-01T00:00:00'
FROM person.Person AS personRow
INNER JOIN @DemoStudents AS demo
    ON demo.PersonId = personRow.PersonId;

SET IDENTITY_INSERT person.Person ON;

INSERT INTO person.Person
(
    PersonId,
    MockPassPersonId,
    IdentityNumberMasked,
    FullName,
    DateOfBirth,
    NationalityCode,
    ResidencyStatusCode,
    OfficialEmail,
    PreferredEmail,
    OfficialMobile,
    PreferredMobile,
    OfficialAddress,
    PreferredAddress,
    PersonStatusCode,
    SourceUpdatedAt,
    CreatedAt,
    UpdatedAt
)
SELECT
    demo.PersonId,
    CONCAT(N'LOCAL-TOPUP-STUDENT-', demo.StudentNumber),
    CONCAT(N'S****', RIGHT(demo.StudentNumber, 4)),
    demo.FullName,
    demo.DateOfBirth,
    'SG',
    'CITIZEN',
    CONCAT(LOWER(REPLACE(demo.StudentNumber, N'-', N'')), N'@example.test'),
    CONCAT(LOWER(REPLACE(demo.StudentNumber, N'-', N'')), N'@example.test'),
    '+6590000000',
    '+6590000000',
    'Demo address, Singapore',
    'Demo address, Singapore',
    'ACTIVE',
    '2026-06-01T00:00:00',
    '2026-06-01T00:00:00',
    '2026-06-01T00:00:00'
FROM @DemoStudents AS demo
WHERE NOT EXISTS (
    SELECT 1
    FROM person.Person AS personRow
    WHERE personRow.PersonId = demo.PersonId
);

SET IDENTITY_INSERT person.Person OFF;

UPDATE enrollment
SET
    PersonId = demo.PersonId,
    OrganizationId = demo.OrganizationId,
    StudentNumber = demo.StudentNumber,
    AcademicYear = '2026',
    LevelCode = demo.LevelCode,
    ClassCode = demo.ClassCode,
    SchoolingStatusCode = demo.SchoolingStatusCode,
    StatusReasonCode = CASE WHEN demo.SchoolingStatusCode = 'ON_LEAVE' THEN 'ABSENCE' ELSE NULL END,
    StartDate = '2026-01-02',
    EndDate = NULL,
    SourceCode = 'LOCAL_TOPUP_SEED',
    UpdatedAt = '2026-06-01T00:00:00'
FROM person.SchoolEnrollment AS enrollment
INNER JOIN @DemoStudents AS demo
    ON demo.SchoolEnrollmentId = enrollment.SchoolEnrollmentId;

SET IDENTITY_INSERT person.SchoolEnrollment ON;

INSERT INTO person.SchoolEnrollment
(
    SchoolEnrollmentId,
    PersonId,
    OrganizationId,
    StudentNumber,
    AcademicYear,
    LevelCode,
    ClassCode,
    SchoolingStatusCode,
    StatusReasonCode,
    StartDate,
    EndDate,
    SourceCode,
    CreatedAt,
    UpdatedAt
)
SELECT
    demo.SchoolEnrollmentId,
    demo.PersonId,
    demo.OrganizationId,
    demo.StudentNumber,
    '2026',
    demo.LevelCode,
    demo.ClassCode,
    demo.SchoolingStatusCode,
    CASE WHEN demo.SchoolingStatusCode = 'ON_LEAVE' THEN 'ABSENCE' ELSE NULL END,
    '2026-01-02',
    NULL,
    'LOCAL_TOPUP_SEED',
    '2026-06-01T00:00:00',
    '2026-06-01T00:00:00'
FROM @DemoStudents AS demo
WHERE NOT EXISTS (
    SELECT 1
    FROM person.SchoolEnrollment AS enrollment
    WHERE enrollment.SchoolEnrollmentId = demo.SchoolEnrollmentId
);

SET IDENTITY_INSERT person.SchoolEnrollment OFF;

UPDATE accountRow
SET
    PersonId = demo.PersonId,
    AccountNumber = demo.AccountNumber,
    CurrentBalance = demo.CurrentBalance,
    AccountStatusCode = demo.AccountStatusCode,
    OpenedAt = '2026-01-02T00:00:00',
    OpenedByLoginAccountId = @HqAdminId,
    OpeningTypeCode = 'MANUAL',
    OpeningReason = 'Local TopUp demo seed',
    PendingClosureAt = NULL,
    ClosedAt = NULL,
    ClosingTypeCode = NULL,
    ClosingReason = NULL,
    ClosedByLoginAccountId = NULL
FROM account.EducationAccount AS accountRow
INNER JOIN @DemoStudents AS demo
    ON demo.EducationAccountId = accountRow.EducationAccountId;

SET IDENTITY_INSERT account.EducationAccount ON;

INSERT INTO account.EducationAccount
(
    EducationAccountId,
    PersonId,
    AccountNumber,
    CurrentBalance,
    AccountStatusCode,
    OpenedAt,
    OpenedByLoginAccountId,
    OpeningTypeCode,
    OpeningReason,
    PendingClosureAt,
    ClosureExceptionUntil,
    ClosureExceptionReason,
    ClosureExceptionApprovedByLoginAccountId,
    ClosedAt,
    ClosingTypeCode,
    ClosingReason,
    ClosedByLoginAccountId
)
SELECT
    demo.EducationAccountId,
    demo.PersonId,
    demo.AccountNumber,
    demo.CurrentBalance,
    demo.AccountStatusCode,
    '2026-01-02T00:00:00',
    @HqAdminId,
    'MANUAL',
    'Local TopUp demo seed',
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL
FROM @DemoStudents AS demo
WHERE NOT EXISTS (
    SELECT 1
    FROM account.EducationAccount AS accountRow
    WHERE accountRow.EducationAccountId = demo.EducationAccountId
);

SET IDENTITY_INSERT account.EducationAccount OFF;

DELETE FROM payment.PaymentPart WHERE PaymentPartId = @PaymentPartId;
DELETE FROM payment.Payment WHERE PaymentId = @PaymentId;
DELETE FROM fas.FASSubsidy WHERE FASSubsidyId = @FasSubsidyId;
DELETE FROM fas.FASApplication WHERE FASApplicationId = @FasApplicationId;
DELETE FROM fas.CourseFASScheme WHERE CourseFASSchemeId = @CourseFasSchemeId;
DELETE FROM fas.FASTierBenefit WHERE FASTierBenefitId = @FasTierBenefitId;
DELETE FROM fas.FASTier WHERE FASTierId = @FasTierId;
DELETE FROM fas.FASScheme WHERE FASSchemeId = @FasSchemeId;
DELETE FROM billing.BillLine WHERE BillLineId IN (@BillLineOneId, @BillLineTwoId, @BillLineThreeId);
DELETE FROM billing.Bill WHERE BillId IN (@BillOneId, @BillTwoId);
DELETE FROM course.CourseEnrollment WHERE CourseEnrollmentId IN (@CourseEnrollmentOneId, @CourseEnrollmentTwoId);
DELETE FROM course.CourseFee WHERE CourseFeeId IN (@CourseFeeOneTuitionId, @CourseFeeOneMaterialId, @CourseFeeTwoTuitionId);
DELETE FROM course.FeeComponent WHERE FeeComponentId IN (@FeeComponentTuitionId, @FeeComponentMaterialId);
DELETE FROM course.Course WHERE CourseId IN (@CourseIdOne, @CourseIdTwo);

SET IDENTITY_INSERT course.Course ON;

INSERT INTO course.Course
(
    CourseId,
    OrganizationId,
    CourseCode,
    CourseName,
    Description,
    StartDate,
    EndDate,
    EnrollmentOpenAt,
    EnrollmentCloseAt,
    CourseStatusCode,
    CreatedByLoginAccountId,
    UpdatedByLoginAccountId,
    UpdatedAt,
    DisabledAt,
    DisabledByLoginAccountId
)
VALUES
(
    @CourseIdOne,
    @SchoolOrganizationId,
    N'DEMO-MATH-2026',
    N'Sec 3 Mathematics Bridging',
    N'Local demo course with fees, enrollments, FAS, billing and payment records.',
    '2026-07-01',
    '2026-09-30',
    '2026-06-01T00:00:00',
    '2026-06-30T23:59:59',
    'PUBLISHED',
    @SchoolAdminId,
    @SchoolAdminId,
    '2026-06-01T00:00:00',
    NULL,
    NULL
),
(
    @CourseIdTwo,
    @HqOrganizationId,
    N'DEMO-SCI-2026',
    N'Applied Science Lab Series',
    N'HQ demo course for cross-organization browsing and billing state.',
    '2026-08-01',
    '2026-10-31',
    '2026-06-01T00:00:00',
    '2026-07-15T23:59:59',
    'PUBLISHED',
    @HqAdminId,
    @HqAdminId,
    '2026-06-01T00:00:00',
    NULL,
    NULL
);

SET IDENTITY_INSERT course.Course OFF;

SET IDENTITY_INSERT course.FeeComponent ON;

INSERT INTO course.FeeComponent
(
    FeeComponentId,
    ComponentCode,
    ComponentName,
    ComponentTypeCode,
    CalculationTypeCode,
    IsActive,
    IsTaxComponent
)
VALUES
(@FeeComponentTuitionId, N'DEMO-TUITION', N'Tuition fee', 'COURSE_FEE', 'FIXED_AMOUNT', 1, 0),
(@FeeComponentMaterialId, N'DEMO-MATERIAL', N'Material fee', 'COURSE_FEE', 'FIXED_AMOUNT', 1, 0);

SET IDENTITY_INSERT course.FeeComponent OFF;

SET IDENTITY_INSERT course.CourseFee ON;

INSERT INTO course.CourseFee
(
    CourseFeeId,
    CourseId,
    FeeComponentId,
    FeeValue,
    SequenceNumber,
    IsActive
)
VALUES
(@CourseFeeOneTuitionId, @CourseIdOne, @FeeComponentTuitionId, 320.00, 1, 1),
(@CourseFeeOneMaterialId, @CourseIdOne, @FeeComponentMaterialId, 45.00, 2, 1),
(@CourseFeeTwoTuitionId, @CourseIdTwo, @FeeComponentTuitionId, 280.00, 1, 1);

SET IDENTITY_INSERT course.CourseFee OFF;

SET IDENTITY_INSERT course.CourseEnrollment ON;

INSERT INTO course.CourseEnrollment
(
    CourseEnrollmentId,
    PersonId,
    CourseId,
    EnrollmentSourceCode,
    EnrolledByLoginAccountId,
    EnrolledAt,
    EnrollmentStatusCode,
    ExitAt,
    ExitReasonCode
)
VALUES
(@CourseEnrollmentOneId, 21001, @CourseIdOne, 'ADMIN', @SchoolAdminId, '2026-06-05T09:00:00', 'PENDING_PAYMENT', NULL, NULL),
(@CourseEnrollmentTwoId, 21002, @CourseIdOne, 'ADMIN', @SchoolAdminId, '2026-06-05T09:10:00', 'PENDING_PAYMENT', NULL, NULL);

SET IDENTITY_INSERT course.CourseEnrollment OFF;

SET IDENTITY_INSERT billing.Bill ON;

INSERT INTO billing.Bill
(
    BillId,
    BillNumber,
    CourseEnrollmentId,
    IssuedAt,
    DueDate,
    GrossAmount,
    SubsidyAmount,
    NetPayableAmount,
    PaidAmount,
    OutstandingAmount,
    BillStatusCode
)
VALUES
(@BillOneId, N'BILL-DEMO-0001', @CourseEnrollmentOneId, '2026-06-05T09:15:00', '2026-07-05', 365.00, 160.00, 205.00, 205.00, 0.00, 'PAID'),
(@BillTwoId, N'BILL-DEMO-0002', @CourseEnrollmentTwoId, '2026-06-05T09:20:00', '2026-07-05', 365.00, 0.00, 365.00, 0.00, 365.00, 'OUTSTANDING');

SET IDENTITY_INSERT billing.Bill OFF;

SET IDENTITY_INSERT billing.BillLine ON;

INSERT INTO billing.BillLine
(
    BillLineId,
    BillId,
    CourseFeeId,
    FeeComponentId,
    DescriptionSnapshot,
    Quantity,
    UnitAmount,
    GrossAmount,
    SubsidyAmount,
    NetAmount
)
VALUES
(@BillLineOneId, @BillOneId, @CourseFeeOneTuitionId, @FeeComponentTuitionId, N'Tuition fee', 1.0000, 320.0000, 320.00, 160.00, 160.00),
(@BillLineTwoId, @BillOneId, @CourseFeeOneMaterialId, @FeeComponentMaterialId, N'Material fee', 1.0000, 45.0000, 45.00, 0.00, 45.00),
(@BillLineThreeId, @BillTwoId, @CourseFeeOneTuitionId, @FeeComponentTuitionId, N'Tuition fee', 1.0000, 320.0000, 320.00, 0.00, 320.00);

SET IDENTITY_INSERT billing.BillLine OFF;

SET IDENTITY_INSERT fas.FASScheme ON;

INSERT INTO fas.FASScheme
(
    FASSchemeId,
    SchemeCode,
    SchemeName,
    Description,
    ProviderName,
    EffectiveFrom,
    EffectiveTo,
    ApplicationOpenFrom,
    ApplicationOpenTo,
    SchemeStatusCode
)
VALUES
(@FasSchemeId, N'DEMO-FAS-2026', N'Demo Financial Assistance Scheme', N'Deterministic FAS scheme for local demo flows.', N'MOE', '2026-01-01', '2026-12-31', '2026-01-01', '2026-12-31', 'ACTIVE');

SET IDENTITY_INSERT fas.FASScheme OFF;

SET IDENTITY_INSERT fas.FASTier ON;

INSERT INTO fas.FASTier
(
    FASTierId,
    FASSchemeId,
    TierCode,
    TierName,
    PriorityNumber,
    StatusCode
)
VALUES
(@FasTierId, @FasSchemeId, N'DEMO-TIER-A', N'Full assistance', 1, 'ACTIVE');

SET IDENTITY_INSERT fas.FASTier OFF;

SET IDENTITY_INSERT fas.FASTierBenefit ON;

INSERT INTO fas.FASTierBenefit
(
    FASTierBenefitId,
    FASTierId,
    FeeComponentId,
    SubsidyTypeCode,
    SubsidyValue,
    MaximumSubsidyAmount,
    IsActive
)
VALUES
(@FasTierBenefitId, @FasTierId, @FeeComponentTuitionId, 'PERCENTAGE', 50.0000, 200.00, 1);

SET IDENTITY_INSERT fas.FASTierBenefit OFF;

SET IDENTITY_INSERT fas.CourseFASScheme ON;

INSERT INTO fas.CourseFASScheme
(
    CourseFASSchemeId,
    CourseId,
    FASSchemeId,
    StatusCode
)
VALUES
(@CourseFasSchemeId, @CourseIdOne, @FasSchemeId, 'ACTIVE');

SET IDENTITY_INSERT fas.CourseFASScheme OFF;

SET IDENTITY_INSERT fas.FASApplication ON;

INSERT INTO fas.FASApplication
(
    FASApplicationId,
    ApplicationNumber,
    PersonId,
    FASSchemeId,
    CourseId,
    ApplicationStatusCode,
    NationalitySnapshot,
    HouseholdIncomeSnapshot,
    HouseholdSizeSnapshot,
    PerCapitaIncomeSnapshot,
    EvaluationResultCode,
    SelectedTierId,
    EvaluatedAt,
    ApplicantConfirmedAt,
    SubmittedAt
)
VALUES
(@FasApplicationId, N'FAS-DEMO-0001', 21001, @FasSchemeId, @CourseIdOne, 'APPROVED', N'SG', 1800.00, 4, 450.00, 'ELIGIBLE', @FasTierId, '2026-06-05T08:30:00', '2026-06-05T08:45:00', '2026-06-05T08:20:00');

SET IDENTITY_INSERT fas.FASApplication OFF;

SET IDENTITY_INSERT fas.FASSubsidy ON;

INSERT INTO fas.FASSubsidy
(
    FASSubsidyId,
    FASApplicationId,
    BillLineId,
    FASTierBenefitId,
    GrossAmountSnapshot,
    CalculatedAmount,
    AppliedAmount,
    AppliedAt,
    SubsidyStatusCode
)
VALUES
(@FasSubsidyId, @FasApplicationId, @BillLineOneId, @FasTierBenefitId, 320.00, 160.00, 160.00, '2026-06-05T09:16:00', 'APPLIED');

SET IDENTITY_INSERT fas.FASSubsidy OFF;

SET IDENTITY_INSERT payment.Payment ON;

INSERT INTO payment.Payment
(
    PaymentId,
    BillId,
    PayerPersonId,
    PaymentNumber,
    PaymentAmount,
    SuccessfulAmount,
    PaymentStatusCode,
    ReceiptNumber,
    IdempotencyKey,
    InitiatedAt,
    CompletedAt
)
VALUES
(@PaymentId, @BillOneId, 21001, N'PAY-DEMO-0001', 205.00, 205.00, 'COMPLETED', N'RCT-DEMO-0001', N'LOCAL-PAYMENT-0001', '2026-06-05T09:30:00', '2026-06-05T09:31:00');

SET IDENTITY_INSERT payment.Payment OFF;

SET IDENTITY_INSERT payment.PaymentPart ON;

INSERT INTO payment.PaymentPart
(
    PaymentPartId,
    PaymentId,
    SequenceNumber,
    PaymentMethodCode,
    EducationAccountId,
    PartAmount,
    PartStatusCode,
    ProviderCode,
    ProviderReference,
    AuthorizedAt,
    SettledAt,
    AccountTransactionId,
    FailureReason
)
VALUES
(@PaymentPartId, @PaymentId, 1, 'EDUCATION_ACCOUNT', 41001, 205.00, 'SETTLED', 'LOCAL', N'LOCAL-PART-0001', '2026-06-05T09:30:30', '2026-06-05T09:31:00', NULL, NULL);

SET IDENTITY_INSERT payment.PaymentPart OFF;

DECLARE @ExistingCampaignIds TABLE
(
    TopUpCampaignId bigint PRIMARY KEY
);

INSERT INTO @ExistingCampaignIds (TopUpCampaignId)
SELECT TopUpCampaignId
FROM topup.TopUpCampaign
WHERE CampaignCode IN (N'LOCAL-HQ-TOPUP', N'LOCAL-SCHOOL-TOPUP')
   OR TopUpCampaignId IN (@HqCampaignId, @SchoolCampaignId);

DECLARE @ExistingRunIds TABLE
(
    TopUpRunId bigint PRIMARY KEY
);

INSERT INTO @ExistingRunIds (TopUpRunId)
SELECT run.TopUpRunId
FROM topup.TopUpRun AS run
WHERE run.TopUpCampaignId IN (
    SELECT TopUpCampaignId
    FROM @ExistingCampaignIds
)
OR run.TopUpRunId IN (
    @CompletedRunId,
    @PartialRunId,
    @ProcessingRunId,
    @FailedRunId
);

IF OBJECT_ID(N'topup.TopUpTransaction', N'U') IS NOT NULL
BEGIN
    DELETE transactionRow
    FROM topup.TopUpTransaction AS transactionRow
    INNER JOIN @ExistingRunIds AS existingRun
        ON existingRun.TopUpRunId = transactionRow.TopUpRunId;
END;

DELETE run
FROM topup.TopUpRun AS run
INNER JOIN @ExistingRunIds AS existingRun
    ON existingRun.TopUpRunId = run.TopUpRunId;

IF OBJECT_ID(N'topup.TopUpCampaignRecipient', N'U') IS NOT NULL
BEGIN
    DELETE recipient
    FROM topup.TopUpCampaignRecipient AS recipient
    INNER JOIN @ExistingCampaignIds AS existingCampaign
        ON existingCampaign.TopUpCampaignId = recipient.TopUpCampaignId;
END;

IF OBJECT_ID(N'topup.TopUpCampaignRule', N'U') IS NOT NULL
BEGIN
    DELETE ruleRow
    FROM topup.TopUpCampaignRule AS ruleRow
    INNER JOIN @ExistingCampaignIds AS existingCampaign
        ON existingCampaign.TopUpCampaignId = ruleRow.TopUpCampaignId;
END;

DELETE campaign
FROM topup.TopUpCampaign AS campaign
INNER JOIN @ExistingCampaignIds AS existingCampaign
    ON existingCampaign.TopUpCampaignId = campaign.TopUpCampaignId;

SET IDENTITY_INSERT topup.TopUpCampaign ON;

INSERT INTO topup.TopUpCampaign
(
    TopUpCampaignId,
    OrganizationId,
    CampaignCode,
    CampaignName,
    Description,
    RecipientModeCode,
    DefaultTopUpAmount,
    Reason,
    ScheduleTypeCode,
    StartDate,
    EndDate,
    FrequencyCode,
    FrequencyInterval,
    NextRunAt,
    CampaignStatusCode,
    CampaignVersion,
    CreatedByLoginAccountId,
    CreatedAt,
    UpdatedByLoginAccountId,
    UpdatedAt
)
VALUES
(
    @HqCampaignId,
    @HqOrganizationId,
    N'LOCAL-HQ-TOPUP',
    N'Local HQ Education Top-Up',
    N'Deterministic HQ campaign for local API development.',
    'FixedSelection',
    100.00,
    N'Canonical local top-up seed',
    'Immediate',
    '2026-06-01',
    NULL,
    NULL,
    NULL,
    NULL,
    'ACTIVE',
    1,
    @HqAdminId,
    '2026-06-01T00:00:00',
    @HqAdminId,
    '2026-06-01T00:00:00'
),
(
    @SchoolCampaignId,
    @SchoolOrganizationId,
    N'LOCAL-SCHOOL-TOPUP',
    N'Local School Education Top-Up',
    N'Deterministic school campaign for organization-scope testing.',
    'FixedSelection',
    100.00,
    N'Canonical local top-up seed',
    'Immediate',
    '2026-06-01',
    NULL,
    NULL,
    NULL,
    NULL,
    'ACTIVE',
    1,
    @SchoolAdminId,
    '2026-06-01T00:00:00',
    @SchoolAdminId,
    '2026-06-01T00:00:00'
);

SET IDENTITY_INSERT topup.TopUpCampaign OFF;

INSERT INTO topup.TopUpCampaignRecipient
(
    TopUpCampaignId,
    EducationAccountId,
    AmountOverride,
    IsActive,
    AddedByLoginAccountId,
    AddedAt
)
SELECT
    @HqCampaignId,
    demo.EducationAccountId,
    CASE WHEN demo.EducationAccountId = 41008 THEN 125.00 ELSE NULL END,
    1,
    @HqAdminId,
    '2026-06-01T00:05:00'
FROM @DemoStudents AS demo
WHERE demo.OrganizationId = @HqOrganizationId
  AND NOT EXISTS (
      SELECT 1
      FROM topup.TopUpCampaignRecipient AS recipient
      WHERE recipient.TopUpCampaignId = @HqCampaignId
        AND recipient.EducationAccountId = demo.EducationAccountId
  );

INSERT INTO topup.TopUpCampaignRecipient
(
    TopUpCampaignId,
    EducationAccountId,
    AmountOverride,
    IsActive,
    AddedByLoginAccountId,
    AddedAt
)
SELECT
    @SchoolCampaignId,
    demo.EducationAccountId,
    CASE WHEN demo.EducationAccountId = 41002 THEN 80.00 ELSE NULL END,
    1,
    @SchoolAdminId,
    '2026-06-01T00:05:00'
FROM @DemoStudents AS demo
WHERE demo.OrganizationId = @SchoolOrganizationId
  AND NOT EXISTS (
      SELECT 1
      FROM topup.TopUpCampaignRecipient AS recipient
      WHERE recipient.TopUpCampaignId = @SchoolCampaignId
        AND recipient.EducationAccountId = demo.EducationAccountId
  );

SET IDENTITY_INSERT topup.TopUpRun ON;

INSERT INTO topup.TopUpRun
(
    TopUpRunId,
    TopUpCampaignId,
    CampaignVersion,
    ScheduledFor,
    TriggerTypeCode,
    TriggeredByLoginAccountId,
    RunStatusCode,
    RuleSnapshotJson,
    TotalSelected,
    TotalProcessed,
    TotalSucceeded,
    TotalFailed,
    TotalSkipped,
    TotalAmount,
    StartedAt,
    CompletedAt,
    IdempotencyKey,
    Note
)
VALUES
(
    @CompletedRunId,
    @HqCampaignId,
    1,
    '2026-06-10T01:00:00',
    'MANUAL',
    @HqAdminId,
    'COMPLETED',
    NULL,
    3,
    3,
    3,
    0,
    0,
    300.00,
    '2026-06-10T01:00:05',
    '2026-06-10T01:01:00',
    N'LOCAL-RUN-COMPLETED',
    N'All recipients credited successfully.'
),
(
    @PartialRunId,
    @SchoolCampaignId,
    1,
    '2026-06-11T02:00:00',
    'SCHEDULED',
    NULL,
    'PARTIAL',
    NULL,
    4,
    4,
    2,
    1,
    1,
    200.00,
    '2026-06-11T02:00:05',
    '2026-06-11T02:02:00',
    N'LOCAL-RUN-PARTIAL',
    N'Two credited, one failed, one skipped.'
),
(
    @ProcessingRunId,
    @HqCampaignId,
    1,
    '2026-06-12T03:00:00',
    'SCHEDULED',
    NULL,
    'PROCESSING',
    NULL,
    5,
    2,
    2,
    0,
    0,
    200.00,
    '2026-06-12T03:00:05',
    NULL,
    N'LOCAL-RUN-PROCESSING',
    N'Run currently processing.'
),
(
    @FailedRunId,
    @SchoolCampaignId,
    1,
    '2026-06-13T04:00:00',
    'MANUAL',
    @SchoolAdminId,
    'FAILED',
    NULL,
    2,
    2,
    0,
    2,
    0,
    0.00,
    '2026-06-13T04:00:05',
    '2026-06-13T04:01:00',
    N'LOCAL-RUN-FAILED',
    N'No recipient was credited.'
);

SET IDENTITY_INSERT topup.TopUpRun OFF;

INSERT INTO topup.TopUpTransaction
(
    TopUpRunId,
    EducationAccountId,
    IdempotencyKey,
    TransactionStatusCode,
    Amount,
    AccountTransactionId,
    Reason,
    CreatedAt,
    CompletedAt
)
VALUES
(@CompletedRunId, 41007, N'LOCAL-TXN-96001-41007', 'COMPLETED', 100.00, 97001, NULL, '2026-06-10T01:00:10', '2026-06-10T01:00:30'),
(@CompletedRunId, 41008, N'LOCAL-TXN-96001-41008', 'COMPLETED', 125.00, 97002, NULL, '2026-06-10T01:00:12', '2026-06-10T01:00:34'),
(@CompletedRunId, 41009, N'LOCAL-TXN-96001-41009', 'COMPLETED', 75.00, 97003, NULL, '2026-06-10T01:00:14', '2026-06-10T01:00:40'),
(@PartialRunId, 41001, N'LOCAL-TXN-96002-41001', 'COMPLETED', 100.00, 97004, NULL, '2026-06-11T02:00:10', '2026-06-11T02:00:30'),
(@PartialRunId, 41002, N'LOCAL-TXN-96002-41002', 'COMPLETED', 80.00, 97005, NULL, '2026-06-11T02:00:12', '2026-06-11T02:00:35'),
(@PartialRunId, 41003, N'LOCAL-TXN-96002-41003', 'FAILED', 0.00, NULL, N'Account temporarily unavailable', '2026-06-11T02:00:14', '2026-06-11T02:01:00'),
(@PartialRunId, 41004, N'LOCAL-TXN-96002-41004', 'SKIPPED', 0.00, NULL, N'Eligibility changed before execution', '2026-06-11T02:00:16', '2026-06-11T02:01:05'),
(@FailedRunId, 41005, N'LOCAL-TXN-96004-41005', 'FAILED', 0.00, NULL, N'Credit service unavailable', '2026-06-13T04:00:10', '2026-06-13T04:00:45'),
(@FailedRunId, 41006, N'LOCAL-TXN-96004-41006', 'FAILED', 0.00, NULL, N'Credit service unavailable', '2026-06-13T04:00:12', '2026-06-13T04:00:48');

COMMIT TRANSACTION;

SELECT
    run.TopUpRunId AS RunId,
    campaign.CampaignCode,
    campaign.OrganizationId,
    run.ScheduledFor AS RunDateUtc,
    run.TriggerTypeCode AS TriggerType,
    run.RunStatusCode AS Status,
    run.TotalSelected AS MatchedCount,
    run.TotalProcessed AS ProcessedCount,
    run.TotalSucceeded AS SucceededCount,
    run.TotalFailed AS FailedCount,
    run.TotalSkipped AS SkippedCount,
    run.TotalAmount AS TotalCredited
FROM topup.TopUpRun AS run
INNER JOIN topup.TopUpCampaign AS campaign
    ON campaign.TopUpCampaignId = run.TopUpCampaignId
WHERE run.TopUpRunId IN (
    @CompletedRunId,
    @PartialRunId,
    @ProcessingRunId,
    @FailedRunId
)
ORDER BY run.TopUpRunId;
