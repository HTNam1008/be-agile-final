/*
    B-005 campaign/run history manual-test data

    Prerequisites:
      1. Apply all current EF Core migrations.
      2. Run against the StudentFinance database.
      3. Use the seeded HQ admin account (LoginAccountId = 1001).

    The script is idempotent: it removes and recreates only B005-* data.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @HqAdminId bigint = 1001;
DECLARE @SchoolAdminId bigint = 1002;
DECLARE @HqOrganizationId bigint = 1;
DECLARE @SchoolOrganizationId bigint = 2;

IF OBJECT_ID(N'topup.TopUpCampaign', N'U') IS NULL
    THROW 50001, 'Table topup.TopUpCampaign is missing. Apply migrations first.', 1;

IF OBJECT_ID(N'topup.TopUpRun', N'U') IS NULL
    THROW 50002, 'Table topup.TopUpRun is missing. Apply migrations first.', 1;

IF OBJECT_ID(N'topup.TopUpTransaction', N'U') IS NULL
    THROW 50003, 'Table topup.TopUpTransaction is missing. Apply migrations first.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM iam.LoginAccount
    WHERE LoginAccountId = @HqAdminId
)
    THROW 50004, 'Seeded HQ admin LoginAccountId 1001 is missing.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM account.EducationAccount
    WHERE EducationAccountId IN (4002, 4003)
)
    THROW 50005, 'Demo EducationAccount IDs 4002/4003 are missing.', 1;

DECLARE @ExistingRunIds TABLE (TopUpRunId bigint PRIMARY KEY);

INSERT INTO @ExistingRunIds (TopUpRunId)
SELECT run.TopUpRunId
FROM topup.TopUpRun run
INNER JOIN topup.TopUpCampaign campaign
    ON campaign.TopUpCampaignId = run.TopUpCampaignId
WHERE campaign.CampaignCode LIKE 'B005-%';

DELETE transactionRow
FROM topup.TopUpTransaction transactionRow
INNER JOIN @ExistingRunIds existingRun
    ON existingRun.TopUpRunId = transactionRow.TopUpRunId;

DELETE run
FROM topup.TopUpRun run
INNER JOIN @ExistingRunIds existingRun
    ON existingRun.TopUpRunId = run.TopUpRunId;

DELETE FROM topup.TopUpCampaign
WHERE CampaignCode LIKE 'B005-%';

DECLARE @Campaigns TABLE
(
    CampaignCode nvarchar(50) PRIMARY KEY,
    TopUpCampaignId bigint NOT NULL
);

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
OUTPUT inserted.CampaignCode, inserted.TopUpCampaignId
INTO @Campaigns (CampaignCode, TopUpCampaignId)
VALUES
(
    @HqOrganizationId,
    N'B005-HQ-ACTIVE',
    N'B005 HQ Active Campaign',
    N'Active HQ campaign for history filtering.',
    'FixedSelection',
    50.00,
    N'B-005 manual test',
    'Recurring',
    '2026-05-01',
    '2026-12-31',
    'Monthly',
    1,
    '2026-07-01T00:00:00',
    'Active',
    3,
    @HqAdminId,
    '2026-05-01T01:00:00',
    @HqAdminId,
    '2026-06-15T01:00:00'
),
(
    @HqOrganizationId,
    N'B005-HQ-DRAFT',
    N'B005 HQ Draft Campaign',
    N'Draft HQ campaign for status filtering.',
    'DynamicRules',
    75.00,
    N'B-005 manual test',
    'Immediate',
    '2026-06-10',
    NULL,
    NULL,
    NULL,
    NULL,
    'Draft',
    1,
    @HqAdminId,
    '2026-06-10T02:00:00',
    @HqAdminId,
    '2026-06-10T02:00:00'
),
(
    @SchoolOrganizationId,
    N'B005-SCHOOL-PAUSED',
    N'B005 School Paused Campaign',
    N'Paused school campaign proving HQ_ADMIN global visibility.',
    'FixedSelection',
    30.00,
    N'B-005 manual test',
    'OneTimeScheduled',
    '2026-06-05',
    NULL,
    NULL,
    NULL,
    NULL,
    'Paused',
    2,
    @SchoolAdminId,
    '2026-05-20T03:00:00',
    @HqAdminId,
    '2026-06-12T03:00:00'
),
(
    @SchoolOrganizationId,
    N'B005-SCHOOL-CANCELLED',
    N'B005 School Cancelled Campaign',
    N'Cancelled school campaign for date and actor filters.',
    'FixedSelection',
    40.00,
    N'B-005 manual test',
    'Immediate',
    '2026-04-01',
    NULL,
    NULL,
    NULL,
    NULL,
    'Cancelled',
    4,
    @SchoolAdminId,
    '2026-04-01T04:00:00',
    @SchoolAdminId,
    '2026-05-01T04:00:00'
);

DECLARE @HqActiveCampaignId bigint =
(
    SELECT TopUpCampaignId
    FROM @Campaigns
    WHERE CampaignCode = N'B005-HQ-ACTIVE'
);

DECLARE @SchoolPausedCampaignId bigint =
(
    SELECT TopUpCampaignId
    FROM @Campaigns
    WHERE CampaignCode = N'B005-SCHOOL-PAUSED'
);

DECLARE @SchoolCancelledCampaignId bigint =
(
    SELECT TopUpCampaignId
    FROM @Campaigns
    WHERE CampaignCode = N'B005-SCHOOL-CANCELLED'
);

DECLARE @Runs TABLE
(
    RunKey nvarchar(50) PRIMARY KEY,
    TopUpRunId bigint NOT NULL
);

INSERT INTO topup.TopUpRun
(
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
    TotalAmount,
    StartedAt,
    CompletedAt,
    IdempotencyKey
)
OUTPUT inserted.IdempotencyKey, inserted.TopUpRunId
INTO @Runs (RunKey, TopUpRunId)
VALUES
(
    @HqActiveCampaignId,
    3,
    '2026-06-15T02:00:00',
    'MANUAL',
    @HqAdminId,
    'COMPLETED',
    NULL,
    3,
    3,
    3,
    0,
    150.00,
    '2026-06-15T02:00:05',
    '2026-06-15T02:01:00',
    N'B005-RUN-HQ-COMPLETED'
),
(
    @SchoolPausedCampaignId,
    2,
    '2026-06-12T03:30:00',
    'SCHEDULED',
    NULL,
    'PARTIAL',
    NULL,
    4,
    4,
    2,
    1,
    60.00,
    '2026-06-12T03:30:05',
    '2026-06-12T03:32:00',
    N'B005-RUN-SCHOOL-PARTIAL'
),
(
    @SchoolCancelledCampaignId,
    4,
    '2026-05-01T04:30:00',
    'MANUAL',
    @SchoolAdminId,
    'FAILED',
    NULL,
    2,
    2,
    0,
    2,
    0.00,
    '2026-05-01T04:30:05',
    '2026-05-01T04:31:00',
    N'B005-RUN-SCHOOL-FAILED'
),
(
    @HqActiveCampaignId,
    3,
    '2026-06-18T05:00:00',
    'SCHEDULED',
    NULL,
    'PROCESSING',
    NULL,
    5,
    2,
    2,
    0,
    100.00,
    '2026-06-18T05:00:05',
    NULL,
    N'B005-RUN-HQ-PROCESSING'
);

DECLARE @CompletedRunId bigint =
(
    SELECT TopUpRunId
    FROM @Runs
    WHERE RunKey = N'B005-RUN-HQ-COMPLETED'
);

DECLARE @PartialRunId bigint =
(
    SELECT TopUpRunId
    FROM @Runs
    WHERE RunKey = N'B005-RUN-SCHOOL-PARTIAL'
);

INSERT INTO topup.TopUpTransaction
(
    TopUpRunId,
    EducationAccountId,
    TopUpAmount,
    Reason,
    TransactionStatusCode,
    ProcessedByLoginAccountId,
    ProcessedAt,
    FailureReason,
    AccountTransactionId,
    IdempotencyKey
)
VALUES
(
    @CompletedRunId,
    4002,
    50.00,
    N'B-005 successful recipient',
    'COMPLETED',
    @HqAdminId,
    '2026-06-15T02:00:30',
    NULL,
    NULL,
    CONCAT(N'B005-TX-', @CompletedRunId, N'-4002')
),
(
    @PartialRunId,
    4003,
    0.00,
    N'B-005 failed recipient',
    'FAILED',
    @HqAdminId,
    '2026-06-12T03:31:00',
    N'Demo failure',
    NULL,
    CONCAT(N'B005-TX-', @PartialRunId, N'-4003')
);

COMMIT TRANSACTION;

SELECT
    campaign.TopUpCampaignId,
    campaign.OrganizationId,
    campaign.CampaignCode,
    campaign.CampaignName,
    campaign.CampaignStatusCode,
    campaign.CreatedByLoginAccountId,
    campaign.CreatedAt
FROM topup.TopUpCampaign campaign
WHERE campaign.CampaignCode LIKE 'B005-%'
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
    run.TotalAmount,
    run.TriggeredByLoginAccountId
FROM topup.TopUpRun run
INNER JOIN topup.TopUpCampaign campaign
    ON campaign.TopUpCampaignId = run.TopUpCampaignId
WHERE campaign.CampaignCode LIKE 'B005-%'
ORDER BY run.ScheduledFor DESC, run.TopUpRunId DESC;
