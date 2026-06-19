/*
    B-002 manual-test seed data

    Prerequisites:
      1. Run all EF Core migrations.
      2. Execute this script against the StudentFinance database.

    The script is idempotent. Re-running it resets the B-002 campaign selection
    while preserving any unrelated data.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @OrganizationId bigint = 2;
DECLARE @ActorLoginAccountId bigint = 1001;
DECLARE @CampaignCode nvarchar(50) = N'B002-MANUAL-SEED';
DECLARE @CampaignId bigint;
DECLARE @SeededAt datetime2 = '2026-06-18T00:00:00';

IF OBJECT_ID(N'org.Organization', N'U') IS NULL
    THROW 50001, 'Required table org.Organization does not exist. Run migrations first.', 1;

IF OBJECT_ID(N'person.Person', N'U') IS NULL
    THROW 50002, 'Required table person.Person does not exist. Run migrations first.', 1;

IF OBJECT_ID(N'person.SchoolEnrollment', N'U') IS NULL
    THROW 50003, 'Required table person.SchoolEnrollment does not exist. Run migrations first.', 1;

IF OBJECT_ID(N'account.EducationAccount', N'U') IS NULL
    THROW 50004, 'Required table account.EducationAccount does not exist. Run migrations first.', 1;

IF OBJECT_ID(N'topup.TopUpCampaign', N'U') IS NULL
    THROW 50005, 'Required table topup.TopUpCampaign does not exist. Run migrations first.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM org.Organization
    WHERE OrganizationId = @OrganizationId
)
    THROW 50006, 'Organization 2 is missing. Run the standard demo-data migrations first.', 1;

DECLARE @People TABLE
(
    PersonId bigint NOT NULL,
    ExternalReference nvarchar(100) NOT NULL,
    IdentityNumberMasked nvarchar(30) NOT NULL,
    FullName nvarchar(200) NOT NULL,
    DateOfBirth date NOT NULL,
    Email nvarchar(320) NOT NULL,
    Mobile nvarchar(50) NOT NULL
);

INSERT INTO @People
    (PersonId, ExternalReference, IdentityNumberMasked, FullName, DateOfBirth, Email, Mobile)
VALUES
    (92001, N'B002-PERSON-001', N'S920****A', N'B002 Alice Tan',   '2009-02-10', N'b002.alice@example.test',   N'+6592000001'),
    (92002, N'B002-PERSON-002', N'S920****B', N'B002 Benjamin Lim','2010-07-22', N'b002.benjamin@example.test',N'+6592000002'),
    (92003, N'B002-PERSON-003', N'S920****C', N'B002 Chloe Ng',    '2008-11-05', N'b002.chloe@example.test',   N'+6592000003'),
    (92004, N'B002-PERSON-004', N'S920****D', N'B002 Daniel Goh',  '2009-05-14', N'b002.daniel@example.test',  N'+6592000004');

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
    source.PersonId,
    source.ExternalReference,
    source.IdentityNumberMasked,
    source.FullName,
    source.DateOfBirth,
    'SG',
    'CITIZEN',
    source.Email,
    source.Email,
    source.Mobile,
    source.Mobile,
    N'2 B002 Test Street, Singapore 000002',
    N'2 B002 Test Street, Singapore 000002',
    'ACTIVE',
    @SeededAt,
    @SeededAt,
    @SeededAt
FROM @People source
WHERE NOT EXISTS (
    SELECT 1
    FROM person.Person target
    WHERE target.PersonId = source.PersonId
       OR target.MockPassPersonId = source.ExternalReference
);

SET IDENTITY_INSERT person.Person OFF;

DECLARE @Enrollments TABLE
(
    SchoolEnrollmentId bigint NOT NULL,
    PersonId bigint NOT NULL,
    StudentNumber nvarchar(50) NOT NULL,
    LevelCode varchar(30) NOT NULL,
    ClassCode varchar(30) NOT NULL,
    SchoolingStatusCode varchar(30) NOT NULL
);

INSERT INTO @Enrollments
    (SchoolEnrollmentId, PersonId, StudentNumber, LevelCode, ClassCode, SchoolingStatusCode)
VALUES
    (93001, 92001, N'B002-STU-001', 'SEC_3', '3A', 'ACTIVE'),
    (93002, 92002, N'B002-STU-002', 'SEC_2', '2B', 'ACTIVE'),
    (93003, 92003, N'B002-STU-003', 'SEC_4', '4C', 'ACTIVE'),
    (93004, 92004, N'B002-STU-004', 'SEC_3', '3D', 'ON_LEAVE');

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
    source.SchoolEnrollmentId,
    source.PersonId,
    @OrganizationId,
    source.StudentNumber,
    '2026',
    source.LevelCode,
    source.ClassCode,
    source.SchoolingStatusCode,
    NULL,
    '2026-01-02',
    NULL,
    'B002_SEED',
    @SeededAt,
    @SeededAt
FROM @Enrollments source
WHERE NOT EXISTS (
    SELECT 1
    FROM person.SchoolEnrollment target
    WHERE target.SchoolEnrollmentId = source.SchoolEnrollmentId
       OR target.StudentNumber = source.StudentNumber
);

SET IDENTITY_INSERT person.SchoolEnrollment OFF;

DECLARE @Accounts TABLE
(
    EducationAccountId bigint NOT NULL,
    PersonId bigint NOT NULL,
    AccountNumber nvarchar(50) NOT NULL,
    CurrentBalance decimal(19,2) NOT NULL,
    AccountStatusCode varchar(30) NOT NULL
);

INSERT INTO @Accounts
    (EducationAccountId, PersonId, AccountNumber, CurrentBalance, AccountStatusCode)
VALUES
    (94001, 92001, N'EA-B002-0001',  25.00, 'ACTIVE'),
    (94002, 92002, N'EA-B002-0002', 125.50, 'ACTIVE'),
    (94003, 92003, N'EA-B002-0003', 480.00, 'ACTIVE'),
    (94004, 92004, N'EA-B002-0004',  35.75, 'CLOSING');

SET IDENTITY_INSERT account.EducationAccount ON;

INSERT INTO account.EducationAccount
(
    EducationAccountId,
    PersonId,
    AccountNumber,
    AccountStatusCode,
    OpenedAt,
    OpeningTypeCode,
    OpeningReason,
    OpenedByLoginAccountId,
    PendingClosureAt,
    ClosureExceptionUntil,
    ClosureExceptionReason,
    ClosureExceptionApprovedByLoginAccountId,
    ClosedAt,
    ClosingTypeCode,
    ClosingReason,
    ClosedByLoginAccountId,
    CurrentBalance
)
SELECT
    source.EducationAccountId,
    source.PersonId,
    source.AccountNumber,
    source.AccountStatusCode,
    TODATETIMEOFFSET(@SeededAt, '+00:00'),
    'MANUAL',
    N'B-002 manual-test seed account',
    @ActorLoginAccountId,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    source.CurrentBalance
FROM @Accounts source
WHERE NOT EXISTS (
    SELECT 1
    FROM account.EducationAccount target
    WHERE target.EducationAccountId = source.EducationAccountId
       OR target.PersonId = source.PersonId
       OR target.AccountNumber = source.AccountNumber
);

SET IDENTITY_INSERT account.EducationAccount OFF;

SELECT @CampaignId = TopUpCampaignId
FROM topup.TopUpCampaign
WHERE OrganizationId = @OrganizationId
  AND CampaignCode = @CampaignCode;

IF @CampaignId IS NULL
BEGIN
    INSERT INTO topup.TopUpCampaign
    (
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
        @OrganizationId,
        @CampaignCode,
        N'B-002 Manual Test Campaign',
        N'Fixed-selection campaign for explicit and select-all manual testing.',
        'FixedSelection',
        50.00,
        N'B-002 manual test',
        'Immediate',
        CAST(@SeededAt AS date),
        NULL,
        NULL,
        NULL,
        NULL,
        'DRAFT',
        1,
        @ActorLoginAccountId,
        @SeededAt,
        @ActorLoginAccountId,
        @SeededAt
    );

    SET @CampaignId = SCOPE_IDENTITY();
END
ELSE
BEGIN
    DELETE FROM topup.TopUpCampaignRecipient
    WHERE TopUpCampaignId = @CampaignId;

    UPDATE topup.TopUpCampaign
    SET CampaignName = N'B-002 Manual Test Campaign',
        Description = N'Fixed-selection campaign for explicit and select-all manual testing.',
        RecipientModeCode = 'FixedSelection',
        DefaultTopUpAmount = 50.00,
        Reason = N'B-002 manual test',
        ScheduleTypeCode = 'Immediate',
        StartDate = CAST(@SeededAt AS date),
        EndDate = NULL,
        FrequencyCode = NULL,
        FrequencyInterval = NULL,
        NextRunAt = NULL,
        CampaignStatusCode = 'DRAFT',
        CampaignVersion = CampaignVersion + 1,
        UpdatedByLoginAccountId = @ActorLoginAccountId,
        UpdatedAt = @SeededAt
    WHERE TopUpCampaignId = @CampaignId;
END;

COMMIT TRANSACTION;

SELECT
    @CampaignId AS CampaignId,
    @OrganizationId AS OrganizationId,
    @CampaignCode AS CampaignCode,
    'Use account IDs 94001, 94002, 94003 for ACTIVE selections.' AS TestHint,
    'Use account ID 94004 to test rejection by ACTIVE filters.' AS NegativeTestHint;

SELECT
    account.EducationAccountId,
    account.AccountNumber,
    account.AccountStatusCode,
    account.CurrentBalance,
    enrollment.StudentNumber,
    person.FullName,
    enrollment.SchoolingStatusCode,
    enrollment.LevelCode,
    enrollment.ClassCode,
    enrollment.OrganizationId
FROM account.EducationAccount account
INNER JOIN person.Person person
    ON person.PersonId = account.PersonId
INNER JOIN person.SchoolEnrollment enrollment
    ON enrollment.PersonId = person.PersonId
WHERE account.EducationAccountId BETWEEN 94001 AND 94004
ORDER BY account.EducationAccountId;
