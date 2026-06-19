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

BEGIN TRANSACTION;

DECLARE @HqOrganizationId bigint = 1;
DECLARE @SchoolOrganizationId bigint = 2;
DECLARE @SystemAdminId bigint = 1001;
DECLARE @SchoolAdminId bigint = 1002;

DECLARE @HqCampaignId bigint = 95001;
DECLARE @SchoolCampaignId bigint = 95002;

DECLARE @CompletedRunId bigint = 96001;
DECLARE @PartialRunId bigint = 96002;
DECLARE @ProcessingRunId bigint = 96003;
DECLARE @FailedRunId bigint = 96004;

IF OBJECT_ID(N'topup.TopUpCampaign', N'U') IS NULL
    THROW 50001, 'Table topup.TopUpCampaign is missing. Apply migrations first.', 1;

IF OBJECT_ID(N'topup.TopUpRun', N'U') IS NULL
    THROW 50002, 'Table topup.TopUpRun is missing. Apply migrations first.', 1;

IF COL_LENGTH(N'topup.TopUpRun', N'Note') IS NULL
    THROW 50003, 'Column topup.TopUpRun.Note is missing. Run repair-b003-topup-run-columns.sql.', 1;

IF COL_LENGTH(N'topup.TopUpRun', N'TotalSkipped') IS NULL
    THROW 50004, 'Column topup.TopUpRun.TotalSkipped is missing. Run repair-b003-topup-run-columns.sql.', 1;

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
    WHERE LoginAccountId = @SystemAdminId
)
    THROW 50007, 'Seed system admin 1001 is missing. Apply the standard migrations first.', 1;

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
    @SystemAdminId,
    '2026-06-01T00:00:00',
    @SystemAdminId,
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
    @SystemAdminId,
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
