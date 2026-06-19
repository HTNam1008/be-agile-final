/*
    Consolidated manual-test data for:
      B-001 Scoped Account Search
      B-002 Select-All Filter Support
      B-003 Run Summary
      B-004 Transaction Results
      B-005 Campaign and Run History

    Prerequisites:
      1. Apply all current EF Core migrations.
      2. Run against the StudentFinance SQL Server database.
      3. Standard organizations 1/2 and login accounts 1001/1002 must exist.

    Characteristics:
      - idempotent: only rows using the B01X05-* prefix or reserved IDs below
        are deleted and recreated;
      - deterministic IDs for Swagger/manual testing;
      - unrelated application data is preserved.

    Reserved IDs:
      Person              98101-98110
      SchoolEnrollment    98201-98210
      EducationAccount    98301-98310
      TopUpCampaign       98401-98405
      TopUpRun            98501-98506
      AccountTransaction  98601-98611
      TopUpTransaction    98701-98716
      CampaignRecipient   98801-98804
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @HqOrganizationId bigint = 1;
    DECLARE @SchoolOrganizationId bigint = 2;
    DECLARE @SystemAdminId bigint = 1001;
    DECLARE @SchoolAdminId bigint = 1002;
    DECLARE @SeededAt datetime2 = '2026-06-18T08:00:00';

    DECLARE @SelectionCampaignId bigint = 98401;
    DECLARE @ActiveCampaignId bigint = 98402;
    DECLARE @PausedCampaignId bigint = 98403;
    DECLARE @DraftCampaignId bigint = 98404;
    DECLARE @CancelledCampaignId bigint = 98405;

    DECLARE @CompletedRunId bigint = 98501;
    DECLARE @PartialRunId bigint = 98502;
    DECLARE @ProcessingRunId bigint = 98503;
    DECLARE @FailedRunId bigint = 98504;
    DECLARE @OlderRunId bigint = 98505;
    DECLARE @HqRunId bigint = 98506;

    ---------------------------------------------------------------------------
    -- Preconditions
    ---------------------------------------------------------------------------

    IF OBJECT_ID(N'person.Person', N'U') IS NULL
        THROW 50101, 'Table person.Person is missing. Apply migrations first.', 1;

    IF OBJECT_ID(N'person.SchoolEnrollment', N'U') IS NULL
        THROW 50102, 'Table person.SchoolEnrollment is missing. Apply migrations first.', 1;

    IF OBJECT_ID(N'account.EducationAccount', N'U') IS NULL
        THROW 50103, 'Table account.EducationAccount is missing. Apply migrations first.', 1;

    IF OBJECT_ID(N'account.AccountTransaction', N'U') IS NULL
        THROW 50104, 'Table account.AccountTransaction is missing. Apply migrations first.', 1;

    IF OBJECT_ID(N'topup.TopUpCampaign', N'U') IS NULL
        THROW 50105, 'Table topup.TopUpCampaign is missing. Apply migrations first.', 1;

    IF OBJECT_ID(N'topup.TopUpRun', N'U') IS NULL
        THROW 50106, 'Table topup.TopUpRun is missing. Apply migrations first.', 1;

    IF OBJECT_ID(N'topup.TopUpTransaction', N'U') IS NULL
        THROW 50107, 'Table topup.TopUpTransaction is missing. Apply migrations first.', 1;

    IF OBJECT_ID(N'topup.TopUpCampaignRecipient', N'U') IS NULL
        THROW 50108, 'Table topup.TopUpCampaignRecipient is missing. Apply migrations first.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM org.Organization
        WHERE OrganizationId = @HqOrganizationId
    )
        THROW 50109, 'Organization 1 is missing.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM org.Organization
        WHERE OrganizationId = @SchoolOrganizationId
    )
        THROW 50110, 'Organization 2 is missing.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM iam.LoginAccount
        WHERE LoginAccountId = @SystemAdminId
    )
        THROW 50111, 'Login account 1001 is missing.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM iam.LoginAccount
        WHERE LoginAccountId = @SchoolAdminId
    )
        THROW 50112, 'Login account 1002 is missing.', 1;

    IF EXISTS (
        SELECT 1
        FROM person.Person
        WHERE PersonId BETWEEN 98101 AND 98110
          AND MockPassPersonId NOT LIKE N'B01X05-PERSON-%'
    )
        THROW 50113, 'Reserved Person IDs 98101-98110 are used by unrelated data.', 1;

    IF EXISTS (
        SELECT 1
        FROM account.EducationAccount
        WHERE EducationAccountId BETWEEN 98301 AND 98310
          AND AccountNumber NOT LIKE N'EA-B01X05-%'
    )
        THROW 50114, 'Reserved EducationAccount IDs 98301-98310 are used by unrelated data.', 1;

    IF EXISTS (
        SELECT 1
        FROM topup.TopUpCampaign
        WHERE TopUpCampaignId BETWEEN 98401 AND 98405
          AND CampaignCode NOT LIKE N'B01X05-%'
    )
        THROW 50115, 'Reserved campaign IDs 98401-98405 are used by unrelated data.', 1;

    IF EXISTS (
        SELECT 1
        FROM topup.TopUpRun
        WHERE TopUpRunId BETWEEN 98501 AND 98506
          AND IdempotencyKey NOT LIKE N'B01X05-RUN-%'
    )
        THROW 50116, 'Reserved run IDs 98501-98506 are used by unrelated data.', 1;

    ---------------------------------------------------------------------------
    -- Idempotent cleanup: children first
    ---------------------------------------------------------------------------

    DELETE FROM topup.TopUpTransaction
    WHERE TopUpTransactionId BETWEEN 98701 AND 98716
       OR TopUpRunId BETWEEN 98501 AND 98506
       OR IdempotencyKey LIKE N'B01X05-TX-%';

    DELETE FROM account.AccountTransaction
    WHERE AccountTransactionId BETWEEN 98601 AND 98611
       OR IdempotencyKey LIKE N'B01X05-LEDGER-%';

    DELETE FROM topup.TopUpCampaignRecipient
    WHERE TopUpCampaignRecipientId BETWEEN 98801 AND 98804
       OR TopUpCampaignId BETWEEN 98401 AND 98405;

    IF OBJECT_ID(N'topup.TopUpCampaignRule', N'U') IS NOT NULL
    BEGIN
        DELETE FROM topup.TopUpCampaignRule
        WHERE TopUpCampaignId BETWEEN 98401 AND 98405;
    END;

    DELETE FROM topup.TopUpRun
    WHERE TopUpRunId BETWEEN 98501 AND 98506
       OR IdempotencyKey LIKE N'B01X05-RUN-%';

    DELETE FROM topup.TopUpCampaign
    WHERE TopUpCampaignId BETWEEN 98401 AND 98405
       OR CampaignCode LIKE N'B01X05-%';

    DELETE FROM account.EducationAccount
    WHERE EducationAccountId BETWEEN 98301 AND 98310
       OR AccountNumber LIKE N'EA-B01X05-%';

    DELETE FROM person.SchoolEnrollment
    WHERE SchoolEnrollmentId BETWEEN 98201 AND 98210
       OR StudentNumber LIKE N'B01X05-STU-%';

    DELETE FROM person.Person
    WHERE PersonId BETWEEN 98101 AND 98110
       OR MockPassPersonId LIKE N'B01X05-PERSON-%';

    ---------------------------------------------------------------------------
    -- B-001/B-002 people, enrollments and Education Accounts
    --
    -- School organization rows 98101-98108:
    --   varied name, student number, age, level, class, schooling state,
    --   account state and balance for server-side filter testing.
    --
    -- HQ rows 98109-98110:
    --   deliberately outside school-admin scope.
    ---------------------------------------------------------------------------

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
    VALUES
    (98101, N'B01X05-PERSON-001', N'S981****A', N'B01X05 Alice Tan',    '2009-02-10', 'SG', 'CITIZEN', N'alice.b01x05@example.test',    N'alice.b01x05@example.test',    N'+6598100001', N'+6598100001', N'1 Seed Street', N'1 Seed Street', 'ACTIVE', @SeededAt, @SeededAt, @SeededAt),
    (98102, N'B01X05-PERSON-002', N'S981****B', N'B01X05 Benjamin Lim', '2010-07-22', 'SG', 'CITIZEN', N'benjamin.b01x05@example.test', N'benjamin.b01x05@example.test', N'+6598100002', N'+6598100002', N'2 Seed Street', N'2 Seed Street', 'ACTIVE', @SeededAt, @SeededAt, @SeededAt),
    (98103, N'B01X05-PERSON-003', N'S981****C', N'B01X05 Chloe Ng',     '2008-11-05', 'SG', 'CITIZEN', N'chloe.b01x05@example.test',    N'chloe.b01x05@example.test',    N'+6598100003', N'+6598100003', N'3 Seed Street', N'3 Seed Street', 'ACTIVE', @SeededAt, @SeededAt, @SeededAt),
    (98104, N'B01X05-PERSON-004', N'S981****D', N'B01X05 Daniel Goh',   '2009-05-14', 'SG', 'CITIZEN', N'daniel.b01x05@example.test',   N'daniel.b01x05@example.test',   N'+6598100004', N'+6598100004', N'4 Seed Street', N'4 Seed Street', 'ACTIVE', @SeededAt, @SeededAt, @SeededAt),
    (98105, N'B01X05-PERSON-005', N'S981****E', N'B01X05 Emma Lee',     '2011-01-30', 'SG', 'CITIZEN', N'emma.b01x05@example.test',     N'emma.b01x05@example.test',     N'+6598100005', N'+6598100005', N'5 Seed Street', N'5 Seed Street', 'ACTIVE', @SeededAt, @SeededAt, @SeededAt),
    (98106, N'B01X05-PERSON-006', N'S981****F', N'B01X05 Farid Rahman', '2008-03-18', 'SG', 'CITIZEN', N'farid.b01x05@example.test',    N'farid.b01x05@example.test',    N'+6598100006', N'+6598100006', N'6 Seed Street', N'6 Seed Street', 'ACTIVE', @SeededAt, @SeededAt, @SeededAt),
    (98107, N'B01X05-PERSON-007', N'S981****G', N'B01X05 Grace Wong',   '2010-09-09', 'SG', 'CITIZEN', N'grace.b01x05@example.test',    N'grace.b01x05@example.test',    N'+6598100007', N'+6598100007', N'7 Seed Street', N'7 Seed Street', 'ACTIVE', @SeededAt, @SeededAt, @SeededAt),
    (98108, N'B01X05-PERSON-008', N'S981****H', N'B01X05 Harish Kumar', '2007-12-01', 'SG', 'CITIZEN', N'harish.b01x05@example.test',   N'harish.b01x05@example.test',   N'+6598100008', N'+6598100008', N'8 Seed Street', N'8 Seed Street', 'ACTIVE', @SeededAt, @SeededAt, @SeededAt),
    (98109, N'B01X05-PERSON-009', N'S981****I', N'B01X05 HQ Irene',     '2009-04-20', 'SG', 'CITIZEN', N'irene.b01x05@example.test',    N'irene.b01x05@example.test',    N'+6598100009', N'+6598100009', N'9 Seed Street', N'9 Seed Street', 'ACTIVE', @SeededAt, @SeededAt, @SeededAt),
    (98110, N'B01X05-PERSON-010', N'S981****J', N'B01X05 HQ Jason',     '2010-06-15', 'SG', 'CITIZEN', N'jason.b01x05@example.test',    N'jason.b01x05@example.test',    N'+6598100010', N'+6598100010', N'10 Seed Street', N'10 Seed Street', 'ACTIVE', @SeededAt, @SeededAt, @SeededAt);

    SET IDENTITY_INSERT person.Person OFF;

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
    VALUES
    (98201, 98101, @SchoolOrganizationId, N'B01X05-STU-0001', '2026', 'SEC_3', '3A', 'ACTIVE',   NULL, '2026-01-02', NULL, 'B01X05_SEED', @SeededAt, @SeededAt),
    (98202, 98102, @SchoolOrganizationId, N'B01X05-STU-0002', '2026', 'SEC_2', '2B', 'ACTIVE',   NULL, '2026-01-02', NULL, 'B01X05_SEED', @SeededAt, @SeededAt),
    (98203, 98103, @SchoolOrganizationId, N'B01X05-STU-0003', '2026', 'SEC_4', '4C', 'ACTIVE',   NULL, '2026-01-02', NULL, 'B01X05_SEED', @SeededAt, @SeededAt),
    (98204, 98104, @SchoolOrganizationId, N'B01X05-STU-0004', '2026', 'SEC_3', '3D', 'ON_LEAVE', 'MEDICAL', '2026-01-02', NULL, 'B01X05_SEED', @SeededAt, @SeededAt),
    (98205, 98105, @SchoolOrganizationId, N'B01X05-STU-0005', '2026', 'SEC_1', '1A', 'ACTIVE',   NULL, '2026-01-02', NULL, 'B01X05_SEED', @SeededAt, @SeededAt),
    (98206, 98106, @SchoolOrganizationId, N'B01X05-STU-0006', '2026', 'SEC_4', '4A', 'ACTIVE',   NULL, '2026-01-02', NULL, 'B01X05_SEED', @SeededAt, @SeededAt),
    (98207, 98107, @SchoolOrganizationId, N'B01X05-STU-0007', '2026', 'SEC_2', '2A', 'ACTIVE',   NULL, '2026-01-02', NULL, 'B01X05_SEED', @SeededAt, @SeededAt),
    (98208, 98108, @SchoolOrganizationId, N'B01X05-STU-0008', '2026', 'SEC_5', '5A', 'GRADUATED',NULL, '2026-01-02', '2026-05-31', 'B01X05_SEED', @SeededAt, @SeededAt),
    (98209, 98109, @HqOrganizationId,     N'B01X05-STU-0009', '2026', 'SEC_3', '3H', 'ACTIVE',   NULL, '2026-01-02', NULL, 'B01X05_SEED', @SeededAt, @SeededAt),
    (98210, 98110, @HqOrganizationId,     N'B01X05-STU-0010', '2026', 'SEC_2', '2H', 'ACTIVE',   NULL, '2026-01-02', NULL, 'B01X05_SEED', @SeededAt, @SeededAt);

    SET IDENTITY_INSERT person.SchoolEnrollment OFF;

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
    VALUES
    (98301, 98101, N'EA-B01X05-0001', 'ACTIVE',  TODATETIMEOFFSET(@SeededAt, '+00:00'), 'MANUAL', N'B01X05 test seed', @SystemAdminId, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,  25.00),
    (98302, 98102, N'EA-B01X05-0002', 'ACTIVE',  TODATETIMEOFFSET(@SeededAt, '+00:00'), 'MANUAL', N'B01X05 test seed', @SystemAdminId, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 125.50),
    (98303, 98103, N'EA-B01X05-0003', 'ACTIVE',  TODATETIMEOFFSET(@SeededAt, '+00:00'), 'MANUAL', N'B01X05 test seed', @SystemAdminId, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 480.00),
    (98304, 98104, N'EA-B01X05-0004', 'CLOSING', TODATETIMEOFFSET(@SeededAt, '+00:00'), 'MANUAL', N'B01X05 test seed', @SystemAdminId, '2026-06-30T00:00:00+00:00', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 35.75),
    (98305, 98105, N'EA-B01X05-0005', 'ACTIVE',  TODATETIMEOFFSET(@SeededAt, '+00:00'), 'MANUAL', N'B01X05 test seed', @SystemAdminId, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,   0.00),
    (98306, 98106, N'EA-B01X05-0006', 'ACTIVE',  TODATETIMEOFFSET(@SeededAt, '+00:00'), 'MANUAL', N'B01X05 test seed', @SystemAdminId, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 999.99),
    (98307, 98107, N'EA-B01X05-0007', 'CLOSED',  TODATETIMEOFFSET(@SeededAt, '+00:00'), 'MANUAL', N'B01X05 test seed', @SystemAdminId, NULL, NULL, NULL, NULL, '2026-06-01T00:00:00+00:00', 'MANUAL', N'B01X05 closed account', @SystemAdminId, 10.00),
    (98308, 98108, N'EA-B01X05-0008', 'ACTIVE',  TODATETIMEOFFSET(@SeededAt, '+00:00'), 'MANUAL', N'B01X05 test seed', @SystemAdminId, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 250.00),
    (98309, 98109, N'EA-B01X05-0009', 'ACTIVE',  TODATETIMEOFFSET(@SeededAt, '+00:00'), 'MANUAL', N'B01X05 HQ out-of-scope seed', @SystemAdminId, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,  60.00),
    (98310, 98110, N'EA-B01X05-0010', 'ACTIVE',  TODATETIMEOFFSET(@SeededAt, '+00:00'), 'MANUAL', N'B01X05 HQ out-of-scope seed', @SystemAdminId, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 360.00);

    SET IDENTITY_INSERT account.EducationAccount OFF;

    ---------------------------------------------------------------------------
    -- B-002/B-005 campaigns
    ---------------------------------------------------------------------------

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
        @SelectionCampaignId, @SchoolOrganizationId,
        N'B01X05-SELECT-ALL', N'B01X05 Select-All Draft Campaign',
        N'B-002 explicit/select-all testing campaign.',
        'FIXED_SELECTION', 50.00, N'B-002 select-all test',
        'IMMEDIATE', '2026-06-01', NULL, NULL, NULL, NULL,
        'DRAFT', 1, @SystemAdminId, '2026-06-01T01:00:00',
        @SystemAdminId, '2026-06-01T01:00:00'
    ),
    (
        @ActiveCampaignId, @SchoolOrganizationId,
        N'B01X05-SCHOOL-ACTIVE', N'B01X05 School Active Campaign',
        N'Active campaign for B-003/B-004/B-005.',
        'FIXED_SELECTION', 100.00, N'B-003 to B-005 test',
        'RECURRING', '2026-05-01', '2026-12-31', 'MONTHLY', 1, '2026-07-01T00:00:00',
        'ACTIVE', 3, @SchoolAdminId, '2026-05-01T02:00:00',
        @SystemAdminId, '2026-06-15T02:00:00'
    ),
    (
        @PausedCampaignId, @SchoolOrganizationId,
        N'B01X05-SCHOOL-PAUSED', N'B01X05 School Paused Campaign',
        N'Paused campaign for B-005 status/history filters.',
        'FIXED_SELECTION', 75.00, N'B-005 paused history test',
        'ONE_TIME_SCHEDULED', '2026-05-10', NULL, NULL, NULL, NULL,
        'PAUSED', 2, @SchoolAdminId, '2026-05-10T03:00:00',
        @SchoolAdminId, '2026-06-10T03:00:00'
    ),
    (
        @DraftCampaignId, @HqOrganizationId,
        N'B01X05-HQ-DRAFT', N'B01X05 HQ Draft Campaign',
        N'Out-of-scope HQ draft campaign for B-005.',
        'DYNAMIC_RULES', 125.00, N'B-005 HQ scope test',
        'IMMEDIATE', '2026-06-05', NULL, NULL, NULL, NULL,
        'DRAFT', 1, @SystemAdminId, '2026-06-05T04:00:00',
        @SystemAdminId, '2026-06-05T04:00:00'
    ),
    (
        @CancelledCampaignId, @SchoolOrganizationId,
        N'B01X05-SCHOOL-CANCELLED', N'B01X05 School Cancelled Campaign',
        N'Older cancelled campaign for date/actor filters.',
        'FIXED_SELECTION', 40.00, N'B-005 cancelled history test',
        'IMMEDIATE', '2026-04-01', NULL, NULL, NULL, NULL,
        'CANCELLED', 4, @SchoolAdminId, '2026-04-01T05:00:00',
        @SchoolAdminId, '2026-05-01T05:00:00'
    );

    SET IDENTITY_INSERT topup.TopUpCampaign OFF;

    ---------------------------------------------------------------------------
    -- Initial B-002 fixed recipient selection.
    -- Re-running the upsert endpoint may replace these rows.
    ---------------------------------------------------------------------------

    SET IDENTITY_INSERT topup.TopUpCampaignRecipient ON;

    INSERT INTO topup.TopUpCampaignRecipient
    (
        TopUpCampaignRecipientId,
        TopUpCampaignId,
        EducationAccountId,
        AmountOverride,
        IsActive,
        AddedByLoginAccountId,
        AddedAt
    )
    VALUES
    (98801, @SelectionCampaignId, 98301, NULL,  1, @SystemAdminId, '2026-06-18T08:10:00'),
    (98802, @SelectionCampaignId, 98302, 60.00, 1, @SystemAdminId, '2026-06-18T08:10:00'),
    (98803, @SelectionCampaignId, 98303, NULL,  1, @SystemAdminId, '2026-06-18T08:10:00'),
    (98804, @SelectionCampaignId, 98305, NULL,  1, @SystemAdminId, '2026-06-18T08:10:00');

    SET IDENTITY_INSERT topup.TopUpCampaignRecipient OFF;

    ---------------------------------------------------------------------------
    -- B-003/B-005 runs
    ---------------------------------------------------------------------------

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
        @CompletedRunId, @ActiveCampaignId, 3,
        '2026-06-15T02:00:00', 'MANUAL', @SchoolAdminId, 'COMPLETED', NULL,
        4, 4, 4, 0, 0, 400.00,
        '2026-06-15T02:00:05', '2026-06-15T02:02:00',
        N'B01X05-RUN-COMPLETED', N'All four recipients completed.'
    ),
    (
        @PartialRunId, @ActiveCampaignId, 3,
        '2026-06-16T03:00:00', 'SCHEDULED', NULL, 'PARTIAL', NULL,
        4, 4, 2, 1, 1, 200.00,
        '2026-06-16T03:00:05', '2026-06-16T03:03:00',
        N'B01X05-RUN-PARTIAL', N'Two completed, one failed, one skipped.'
    ),
    (
        @ProcessingRunId, @ActiveCampaignId, 3,
        '2026-06-18T04:00:00', 'SCHEDULED', NULL, 'PROCESSING', NULL,
        4, 2, 1, 0, 0, 100.00,
        '2026-06-18T04:00:05', NULL,
        N'B01X05-RUN-PROCESSING', N'One completed and one pending.'
    ),
    (
        @FailedRunId, @PausedCampaignId, 2,
        '2026-06-10T05:00:00', 'MANUAL', @SchoolAdminId, 'FAILED', NULL,
        2, 2, 0, 2, 0, 0.00,
        '2026-06-10T05:00:05', '2026-06-10T05:01:00',
        N'B01X05-RUN-FAILED', N'Both recipients failed.'
    ),
    (
        @OlderRunId, @CancelledCampaignId, 4,
        '2026-05-01T06:00:00', 'MANUAL', @SchoolAdminId, 'COMPLETED', NULL,
        2, 2, 2, 0, 0, 80.00,
        '2026-05-01T06:00:05', '2026-05-01T06:01:00',
        N'B01X05-RUN-OLDER', N'Older run for date filtering.'
    ),
    (
        @HqRunId, @DraftCampaignId, 1,
        '2026-06-17T07:00:00', 'MANUAL', @SystemAdminId, 'COMPLETED', NULL,
        2, 2, 2, 0, 0, 250.00,
        '2026-06-17T07:00:05', '2026-06-17T07:01:00',
        N'B01X05-RUN-HQ', N'Out-of-scope HQ run.'
    );

    SET IDENTITY_INSERT topup.TopUpRun OFF;

    ---------------------------------------------------------------------------
    -- Ledger references for completed B-004 rows
    ---------------------------------------------------------------------------

    SET IDENTITY_INSERT account.AccountTransaction ON;

    INSERT INTO account.AccountTransaction
    (
        AccountTransactionId,
        EducationAccountId,
        TransactionTypeCode,
        Amount,
        TransactionAt,
        ReferenceTypeCode,
        ReferenceId,
        IdempotencyKey,
        ReversalOfTransactionId,
        BalanceAfter,
        Description,
        CreatedByLoginAccountId
    )
    VALUES
    (98601, 98301, 'TOP_UP', 100.00, '2026-06-15T02:00:30', 'TOP_UP_RUN', @CompletedRunId, N'B01X05-LEDGER-0001', NULL, 125.00,  N'B01X05 completed credit', @SchoolAdminId),
    (98602, 98302, 'TOP_UP', 100.00, '2026-06-15T02:00:40', 'TOP_UP_RUN', @CompletedRunId, N'B01X05-LEDGER-0002', NULL, 225.50,  N'B01X05 completed credit', @SchoolAdminId),
    (98603, 98303, 'TOP_UP', 100.00, '2026-06-15T02:00:50', 'TOP_UP_RUN', @CompletedRunId, N'B01X05-LEDGER-0003', NULL, 580.00,  N'B01X05 completed credit', @SchoolAdminId),
    (98604, 98305, 'TOP_UP', 100.00, '2026-06-15T02:01:00', 'TOP_UP_RUN', @CompletedRunId, N'B01X05-LEDGER-0004', NULL, 100.00,  N'B01X05 completed credit', @SchoolAdminId),
    (98605, 98301, 'TOP_UP', 100.00, '2026-06-16T03:00:30', 'TOP_UP_RUN', @PartialRunId,   N'B01X05-LEDGER-0005', NULL, 225.00,  N'B01X05 partial-run credit', NULL),
    (98606, 98302, 'TOP_UP', 100.00, '2026-06-16T03:00:40', 'TOP_UP_RUN', @PartialRunId,   N'B01X05-LEDGER-0006', NULL, 325.50,  N'B01X05 partial-run credit', NULL),
    (98607, 98303, 'TOP_UP', 100.00, '2026-06-18T04:00:30', 'TOP_UP_RUN', @ProcessingRunId,N'B01X05-LEDGER-0007', NULL, 680.00,  N'B01X05 processing-run credit', NULL),
    (98608, 98301, 'TOP_UP',  40.00, '2026-05-01T06:00:30', 'TOP_UP_RUN', @OlderRunId,     N'B01X05-LEDGER-0008', NULL,  65.00,  N'B01X05 older-run credit', @SchoolAdminId),
    (98609, 98302, 'TOP_UP',  40.00, '2026-05-01T06:00:40', 'TOP_UP_RUN', @OlderRunId,     N'B01X05-LEDGER-0009', NULL, 165.50,  N'B01X05 older-run credit', @SchoolAdminId),
    (98610, 98309, 'TOP_UP', 125.00, '2026-06-17T07:00:30', 'TOP_UP_RUN', @HqRunId,        N'B01X05-LEDGER-0010', NULL, 185.00,  N'B01X05 HQ credit', @SystemAdminId),
    (98611, 98310, 'TOP_UP', 125.00, '2026-06-17T07:00:40', 'TOP_UP_RUN', @HqRunId,        N'B01X05-LEDGER-0011', NULL, 485.00,  N'B01X05 HQ credit', @SystemAdminId);

    SET IDENTITY_INSERT account.AccountTransaction OFF;

    ---------------------------------------------------------------------------
    -- B-004 per-recipient transaction results.
    -- Includes all statuses, safe reasons, dates and account references.
    ---------------------------------------------------------------------------

    SET IDENTITY_INSERT topup.TopUpTransaction ON;

    INSERT INTO topup.TopUpTransaction
    (
        TopUpTransactionId,
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
    (98701, @CompletedRunId, 98301, N'B01X05-TX-98501-98301', 'COMPLETED', 100.00, 98601, NULL, '2026-06-15T02:00:10', '2026-06-15T02:00:30'),
    (98702, @CompletedRunId, 98302, N'B01X05-TX-98501-98302', 'COMPLETED', 100.00, 98602, NULL, '2026-06-15T02:00:20', '2026-06-15T02:00:40'),
    (98703, @CompletedRunId, 98303, N'B01X05-TX-98501-98303', 'COMPLETED', 100.00, 98603, NULL, '2026-06-15T02:00:30', '2026-06-15T02:00:50'),
    (98704, @CompletedRunId, 98305, N'B01X05-TX-98501-98305', 'COMPLETED', 100.00, 98604, NULL, '2026-06-15T02:00:40', '2026-06-15T02:01:00'),

    (98705, @PartialRunId, 98301, N'B01X05-TX-98502-98301', 'COMPLETED', 100.00, 98605, NULL, '2026-06-16T03:00:10', '2026-06-16T03:00:30'),
    (98706, @PartialRunId, 98302, N'B01X05-TX-98502-98302', 'COMPLETED', 100.00, 98606, NULL, '2026-06-16T03:00:20', '2026-06-16T03:00:40'),
    (98707, @PartialRunId, 98304, N'B01X05-TX-98502-98304', 'SKIPPED',     0.00, NULL, N'Account is pending closure', '2026-06-16T03:00:30', '2026-06-16T03:00:50'),
    (98708, @PartialRunId, 98307, N'B01X05-TX-98502-98307', 'FAILED',      0.00, NULL, N'Credit was rejected by account service', '2026-06-16T03:00:40', '2026-06-16T03:01:00'),

    (98709, @ProcessingRunId, 98303, N'B01X05-TX-98503-98303', 'COMPLETED', 100.00, 98607, NULL, '2026-06-18T04:00:10', '2026-06-18T04:00:30'),
    (98710, @ProcessingRunId, 98306, N'B01X05-TX-98503-98306', 'PENDING',   100.00, NULL, NULL, '2026-06-18T04:00:20', NULL),

    (98711, @FailedRunId, 98304, N'B01X05-TX-98504-98304', 'FAILED', 0.00, NULL, N'Credit service temporarily unavailable', '2026-06-10T05:00:10', '2026-06-10T05:00:30'),
    (98712, @FailedRunId, 98307, N'B01X05-TX-98504-98307', 'FAILED', 0.00, NULL, N'Account is closed', '2026-06-10T05:00:20', '2026-06-10T05:00:40'),

    (98713, @OlderRunId, 98301, N'B01X05-TX-98505-98301', 'COMPLETED', 40.00, 98608, NULL, '2026-05-01T06:00:10', '2026-05-01T06:00:30'),
    (98714, @OlderRunId, 98302, N'B01X05-TX-98505-98302', 'COMPLETED', 40.00, 98609, NULL, '2026-05-01T06:00:20', '2026-05-01T06:00:40'),

    (98715, @HqRunId, 98309, N'B01X05-TX-98506-98309', 'COMPLETED', 125.00, 98610, NULL, '2026-06-17T07:00:10', '2026-06-17T07:00:30'),
    (98716, @HqRunId, 98310, N'B01X05-TX-98506-98310', 'COMPLETED', 125.00, 98611, NULL, '2026-06-17T07:00:20', '2026-06-17T07:00:40');

    SET IDENTITY_INSERT topup.TopUpTransaction OFF;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

-------------------------------------------------------------------------------
-- Verification output and manual-test hints
-------------------------------------------------------------------------------

SELECT
    N'B-001' AS Task,
    N'GET /api/admin/v1/top-up/accounts/search?organizationId=2&page=1&pageSize=5' AS SuggestedRequest,
    N'Search Alice, STU-0002, or EA-B01X05-0003; filter ACTIVE, SEC_3, balances and ages.' AS ExpectedUse;

SELECT
    N'B-002' AS Task,
    @SelectionCampaignId AS CampaignId,
    N'PUT /api/admin/v1/top-up-campaigns/98401/fixed-recipients' AS SuggestedRequest,
    N'Use AllMatchingFilter with organizationId=2/accountStatusCode=ACTIVE; exclude 98302. IDs 98309/98310 are outside scope.' AS ExpectedUse;

SELECT
    N'B-003' AS Task,
    @CompletedRunId AS CompletedRunId,
    @PartialRunId AS PartialRunId,
    @ProcessingRunId AS ProcessingRunId,
    N'GET /api/admin/v1/top-up/runs/{runId}' AS SuggestedRequest;

SELECT
    N'B-004' AS Task,
    @PartialRunId AS RecommendedRunId,
    N'GET /api/admin/v1/top-up/runs/98502/transactions?status=FAILED&studentOrAccountSearch=Grace&reason=rejected&page=1&pageSize=25' AS SuggestedRequest,
    N'Run 98502 has COMPLETED, FAILED and SKIPPED rows. Run 98503 also has PENDING.' AS ExpectedUse;

SELECT
    N'B-005' AS Task,
    N'GET /api/admin/v1/top-up-history/campaigns?organizationId=2&page=1&pageSize=25' AS CampaignHistoryRequest,
    N'GET /api/admin/v1/top-up-history/runs?organizationId=2&status=PARTIAL&page=1&pageSize=25' AS RunHistoryRequest;

SELECT
    account.EducationAccountId,
    account.AccountNumber,
    account.AccountStatusCode,
    account.CurrentBalance,
    enrollment.StudentNumber,
    person.FullName,
    person.DateOfBirth,
    enrollment.SchoolingStatusCode,
    enrollment.LevelCode,
    enrollment.ClassCode,
    enrollment.OrganizationId
FROM account.EducationAccount AS account
INNER JOIN person.Person AS person
    ON person.PersonId = account.PersonId
INNER JOIN person.SchoolEnrollment AS enrollment
    ON enrollment.PersonId = person.PersonId
WHERE account.EducationAccountId BETWEEN 98301 AND 98310
ORDER BY account.EducationAccountId;

SELECT
    campaign.TopUpCampaignId,
    campaign.OrganizationId,
    campaign.CampaignCode,
    campaign.CampaignName,
    campaign.CampaignStatusCode,
    campaign.CampaignVersion,
    campaign.CreatedByLoginAccountId,
    campaign.CreatedAt
FROM topup.TopUpCampaign AS campaign
WHERE campaign.TopUpCampaignId BETWEEN 98401 AND 98405
ORDER BY campaign.CreatedAt DESC, campaign.TopUpCampaignId DESC;

SELECT
    run.TopUpRunId,
    campaign.CampaignCode,
    campaign.OrganizationId,
    run.ScheduledFor,
    run.TriggerTypeCode,
    run.RunStatusCode,
    run.TotalSelected,
    run.TotalProcessed,
    run.TotalSucceeded,
    run.TotalFailed,
    run.TotalSkipped,
    run.TotalAmount
FROM topup.TopUpRun AS run
INNER JOIN topup.TopUpCampaign AS campaign
    ON campaign.TopUpCampaignId = run.TopUpCampaignId
WHERE run.TopUpRunId BETWEEN 98501 AND 98506
ORDER BY run.ScheduledFor DESC, run.TopUpRunId DESC;

SELECT
    transactionRow.TopUpTransactionId,
    transactionRow.TopUpRunId,
    transactionRow.EducationAccountId,
    transactionRow.TransactionStatusCode,
    transactionRow.Amount,
    transactionRow.Reason,
    transactionRow.AccountTransactionId,
    transactionRow.CreatedAt,
    transactionRow.CompletedAt
FROM topup.TopUpTransaction AS transactionRow
WHERE transactionRow.TopUpTransactionId BETWEEN 98701 AND 98716
ORDER BY
    transactionRow.TopUpRunId,
    transactionRow.CreatedAt DESC,
    transactionRow.TopUpTransactionId DESC;
