/*
    LOCAL/TEST DATABASE ONLY.

    Deletes every person outside the preserved PersonId range and removes
    related account, IAM, course, billing, payment, FAS, notification, mail,
    audit and AI rows.

    Intended use: keep the seeded baseline students from PersonId 2001 to 2050,
    then remove every other person row and its related data. Admin login accounts
    with PersonId = NULL are not targeted by this script.

    The script is intentionally blocked until @ConfirmDelete is changed to 1.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ConfirmDelete bit = 0;
DECLARE @KeepPersonIdFrom bigint = 2001;
DECLARE @KeepPersonIdTo bigint = 2050;

IF @ConfirmDelete <> 1
BEGIN
    THROW 51000, 'Delete cancelled. Set @ConfirmDelete = 1 after verifying the target database.', 1;
END;

DECLARE @TargetDatabase sysname = DB_NAME();
PRINT CONCAT(
    'Deleting people outside preserved range in ',
    QUOTENAME(@TargetDatabase),
    '. KeepPersonIdFrom=',
    @KeepPersonIdFrom,
    ', KeepPersonIdTo=',
    @KeepPersonIdTo);

DECLARE @PersonIds TABLE (PersonId bigint NOT NULL PRIMARY KEY);
DECLARE @EducationAccountIds TABLE (EducationAccountId bigint NOT NULL PRIMARY KEY);
DECLARE @LoginAccountIds TABLE (LoginAccountId bigint NOT NULL PRIMARY KEY);
DECLARE @CourseEnrollmentIds TABLE (CourseEnrollmentId bigint NOT NULL PRIMARY KEY);
DECLARE @BillIds TABLE (BillId bigint NOT NULL PRIMARY KEY);
DECLARE @BillingStatementIds TABLE (BillingStatementId bigint NOT NULL PRIMARY KEY);
DECLARE @PaymentIds TABLE (PaymentId bigint NOT NULL PRIMARY KEY);
DECLARE @CheckoutSessionIds TABLE (PaymentCheckoutSessionId bigint NOT NULL PRIMARY KEY);
DECLARE @EnrollmentRefundIds TABLE (EnrollmentRefundId bigint NOT NULL PRIMARY KEY);
DECLARE @FasApplicationIds TABLE (FASApplicationId bigint NOT NULL PRIMARY KEY);
DECLARE @FasApplicationSchemeIds TABLE (FASApplicationSchemeId bigint NOT NULL PRIMARY KEY);
DECLARE @FasDocumentIds TABLE (FASDocumentId bigint NOT NULL PRIMARY KEY);
DECLARE @TopUpRunIds TABLE (TopUpRunId bigint NOT NULL PRIMARY KEY);
DECLARE @TopUpCampaignIds TABLE (TopUpCampaignId bigint NOT NULL PRIMARY KEY);
DECLARE @LifecycleRunIds TABLE (EducationAccountLifecycleRunId bigint NOT NULL PRIMARY KEY);
DECLARE @NotificationIds TABLE (NotificationId bigint NOT NULL PRIMARY KEY);
DECLARE @AiConversationIds TABLE (ConversationId uniqueidentifier NOT NULL PRIMARY KEY);
DECLARE @AiReviewRecordIds TABLE (ReviewRecordId uniqueidentifier NOT NULL PRIMARY KEY);

INSERT INTO @PersonIds (PersonId)
SELECT PersonId
FROM person.Person
WHERE PersonId NOT BETWEEN @KeepPersonIdFrom AND @KeepPersonIdTo;

IF NOT EXISTS (SELECT 1 FROM @PersonIds)
BEGIN
    PRINT 'No target people found. Nothing to delete.';
    RETURN;
END;

IF OBJECT_ID(N'account.EducationAccount', N'U') IS NOT NULL
BEGIN
    INSERT INTO @EducationAccountIds (EducationAccountId)
    SELECT EducationAccountId
    FROM account.EducationAccount
    WHERE PersonId IN (SELECT PersonId FROM @PersonIds);
END;

IF OBJECT_ID(N'iam.LoginAccount', N'U') IS NOT NULL
BEGIN
    INSERT INTO @LoginAccountIds (LoginAccountId)
    SELECT LoginAccountId
    FROM iam.LoginAccount
    WHERE PersonId IN (SELECT PersonId FROM @PersonIds);
END;

IF OBJECT_ID(N'course.CourseEnrollment', N'U') IS NOT NULL
BEGIN
    INSERT INTO @CourseEnrollmentIds (CourseEnrollmentId)
    SELECT CourseEnrollmentId
    FROM course.CourseEnrollment
    WHERE PersonId IN (SELECT PersonId FROM @PersonIds);
END;

IF OBJECT_ID(N'billing.Bill', N'U') IS NOT NULL
BEGIN
    INSERT INTO @BillIds (BillId)
    SELECT BillId
    FROM billing.Bill
    WHERE CourseEnrollmentId IN (SELECT CourseEnrollmentId FROM @CourseEnrollmentIds);
END;

IF OBJECT_ID(N'billing.BillingStatement', N'U') IS NOT NULL
BEGIN
    INSERT INTO @BillingStatementIds (BillingStatementId)
    SELECT BillingStatementId
    FROM billing.BillingStatement
    WHERE PersonId IN (SELECT PersonId FROM @PersonIds);
END;

IF OBJECT_ID(N'payment.Payment', N'U') IS NOT NULL
BEGIN
    INSERT INTO @PaymentIds (PaymentId)
    SELECT PaymentId
    FROM payment.Payment
    WHERE BillId IN (SELECT BillId FROM @BillIds)
       OR BillingStatementId IN (SELECT BillingStatementId FROM @BillingStatementIds)
       OR PayerPersonId IN (SELECT PersonId FROM @PersonIds);
END;

IF OBJECT_ID(N'payment.PaymentCheckoutSession', N'U') IS NOT NULL
BEGIN
    INSERT INTO @CheckoutSessionIds (PaymentCheckoutSessionId)
    SELECT PaymentCheckoutSessionId
    FROM payment.PaymentCheckoutSession
    WHERE PersonId IN (SELECT PersonId FROM @PersonIds)
       OR BillId IN (SELECT BillId FROM @BillIds)
       OR PaymentId IN (SELECT PaymentId FROM @PaymentIds);
END;

IF OBJECT_ID(N'payment.EnrollmentRefund', N'U') IS NOT NULL
BEGIN
    INSERT INTO @EnrollmentRefundIds (EnrollmentRefundId)
    SELECT EnrollmentRefundId
    FROM payment.EnrollmentRefund
    WHERE CourseEnrollmentId IN (SELECT CourseEnrollmentId FROM @CourseEnrollmentIds)
       OR PersonId IN (SELECT PersonId FROM @PersonIds);
END;

IF OBJECT_ID(N'fas.FASApplication', N'U') IS NOT NULL
BEGIN
    INSERT INTO @FasApplicationIds (FASApplicationId)
    SELECT FASApplicationId
    FROM fas.FASApplication
    WHERE PersonId IN (SELECT PersonId FROM @PersonIds)
       OR AccountHolderPersonId IN (SELECT PersonId FROM @PersonIds);
END;

IF OBJECT_ID(N'fas.FASApplicationScheme', N'U') IS NOT NULL
BEGIN
    INSERT INTO @FasApplicationSchemeIds (FASApplicationSchemeId)
    SELECT FASApplicationSchemeId
    FROM fas.FASApplicationScheme
    WHERE FASApplicationId IN (SELECT FASApplicationId FROM @FasApplicationIds);
END;

IF OBJECT_ID(N'fas.FASDocument', N'U') IS NOT NULL
BEGIN
    INSERT INTO @FasDocumentIds (FASDocumentId)
    SELECT FASDocumentId
    FROM fas.FASDocument
    WHERE FASApplicationId IN (SELECT FASApplicationId FROM @FasApplicationIds);
END;

IF OBJECT_ID(N'topup.TopUpTransaction', N'U') IS NOT NULL
BEGIN
    INSERT INTO @TopUpRunIds (TopUpRunId)
    SELECT DISTINCT TopUpRunId
    FROM topup.TopUpTransaction
    WHERE EducationAccountId IN (SELECT EducationAccountId FROM @EducationAccountIds);
END;

IF OBJECT_ID(N'topup.TopUpCampaignRecipient', N'U') IS NOT NULL
BEGIN
    INSERT INTO @TopUpCampaignIds (TopUpCampaignId)
    SELECT DISTINCT TopUpCampaignId
    FROM topup.TopUpCampaignRecipient
    WHERE EducationAccountId IN (SELECT EducationAccountId FROM @EducationAccountIds);
END;

IF OBJECT_ID(N'account.EducationAccountLifecycleRunItem', N'U') IS NOT NULL
BEGIN
    INSERT INTO @LifecycleRunIds (EducationAccountLifecycleRunId)
    SELECT DISTINCT EducationAccountLifecycleRunId
    FROM account.EducationAccountLifecycleRunItem
    WHERE PersonId IN (SELECT PersonId FROM @PersonIds)
       OR EducationAccountId IN (SELECT EducationAccountId FROM @EducationAccountIds);
END;

IF OBJECT_ID(N'communication.Notification', N'U') IS NOT NULL
BEGIN
    INSERT INTO @NotificationIds (NotificationId)
    SELECT NotificationId
    FROM communication.Notification
    WHERE RecipientUserAccountId IN (SELECT LoginAccountId FROM @LoginAccountIds);
END;

IF OBJECT_ID(N'ai.Conversation', N'U') IS NOT NULL
BEGIN
    INSERT INTO @AiConversationIds (ConversationId)
    SELECT Id
    FROM ai.Conversation
    WHERE PersonId IN (SELECT PersonId FROM @PersonIds);
END;

IF OBJECT_ID(N'ai.ReviewRecord', N'U') IS NOT NULL
BEGIN
    INSERT INTO @AiReviewRecordIds (ReviewRecordId)
    SELECT Id
    FROM ai.ReviewRecord
    WHERE PersonId IN (SELECT PersonId FROM @PersonIds);
END;

SELECT 'person.Person' AS Target, COUNT(*) AS RowsToDelete FROM @PersonIds
UNION ALL SELECT 'account.EducationAccount', COUNT(*) FROM @EducationAccountIds
UNION ALL SELECT 'iam.LoginAccount', COUNT(*) FROM @LoginAccountIds
UNION ALL SELECT 'course.CourseEnrollment', COUNT(*) FROM @CourseEnrollmentIds
UNION ALL SELECT 'billing.Bill', COUNT(*) FROM @BillIds
UNION ALL SELECT 'billing.BillingStatement', COUNT(*) FROM @BillingStatementIds
UNION ALL SELECT 'payment.Payment', COUNT(*) FROM @PaymentIds
UNION ALL SELECT 'payment.PaymentCheckoutSession', COUNT(*) FROM @CheckoutSessionIds
UNION ALL SELECT 'payment.EnrollmentRefund', COUNT(*) FROM @EnrollmentRefundIds
UNION ALL SELECT 'fas.FASApplication', COUNT(*) FROM @FasApplicationIds
UNION ALL SELECT 'fas.FASApplicationScheme', COUNT(*) FROM @FasApplicationSchemeIds
UNION ALL SELECT 'topup.TopUpRun', COUNT(*) FROM @TopUpRunIds
UNION ALL SELECT 'topup.TopUpCampaign', COUNT(*) FROM @TopUpCampaignIds
UNION ALL SELECT 'communication.Notification', COUNT(*) FROM @NotificationIds
UNION ALL SELECT 'ai.Conversation', COUNT(*) FROM @AiConversationIds
UNION ALL SELECT 'ai.ReviewRecord', COUNT(*) FROM @AiReviewRecordIds;

DECLARE @InitiallyEnabledForeignKeys TABLE
(
    ObjectId int NOT NULL PRIMARY KEY,
    ParentObjectId int NOT NULL
);

INSERT INTO @InitiallyEnabledForeignKeys (ObjectId, ParentObjectId)
SELECT fk.object_id, fk.parent_object_id
FROM sys.foreign_keys fk
WHERE fk.is_disabled = 0;

DECLARE @Sql nvarchar(max) = N'';

SELECT @Sql = STRING_AGG(
    CAST(N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)
        + N' NOCHECK CONSTRAINT ' + QUOTENAME(fk.name) + N';' AS nvarchar(max)),
    CHAR(10))
FROM sys.foreign_keys fk
JOIN @InitiallyEnabledForeignKeys enabled ON enabled.ObjectId = fk.object_id
JOIN sys.tables t ON t.object_id = fk.parent_object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id;

BEGIN TRY
    BEGIN TRANSACTION;

    IF NULLIF(@Sql, N'') IS NOT NULL
        EXEC sys.sp_executesql @Sql;

    IF OBJECT_ID(N'communication.NotificationRealtimeDelivery', N'U') IS NOT NULL
        DELETE FROM communication.NotificationRealtimeDelivery
        WHERE NotificationId IN (SELECT NotificationId FROM @NotificationIds)
           OR RecipientUserAccountId IN (SELECT LoginAccountId FROM @LoginAccountIds);

    IF OBJECT_ID(N'communication.Notification', N'U') IS NOT NULL
        DELETE FROM communication.Notification
        WHERE NotificationId IN (SELECT NotificationId FROM @NotificationIds);

    IF OBJECT_ID(N'mail.EmailNotification', N'U') IS NOT NULL
        DELETE FROM mail.EmailNotification
        WHERE PersonId IN (SELECT PersonId FROM @PersonIds);

    IF OBJECT_ID(N'ai.Message', N'U') IS NOT NULL
        DELETE FROM ai.Message
        WHERE ConversationId IN (SELECT ConversationId FROM @AiConversationIds);

    IF OBJECT_ID(N'ai.AdminCenterCase', N'U') IS NOT NULL
        DELETE FROM ai.AdminCenterCase
        WHERE PersonId IN (SELECT PersonId FROM @PersonIds)
           OR ReviewRecordId IN (SELECT ReviewRecordId FROM @AiReviewRecordIds);

    IF OBJECT_ID(N'ai.ReviewRecord', N'U') IS NOT NULL
        DELETE FROM ai.ReviewRecord
        WHERE Id IN (SELECT ReviewRecordId FROM @AiReviewRecordIds);

    IF OBJECT_ID(N'ai.Conversation', N'U') IS NOT NULL
        DELETE FROM ai.Conversation
        WHERE Id IN (SELECT ConversationId FROM @AiConversationIds);

    IF OBJECT_ID(N'payment.EnrollmentRefundPart', N'U') IS NOT NULL
        DELETE FROM payment.EnrollmentRefundPart
        WHERE EnrollmentRefundId IN (SELECT EnrollmentRefundId FROM @EnrollmentRefundIds);

    IF OBJECT_ID(N'payment.EnrollmentRefund', N'U') IS NOT NULL
        DELETE FROM payment.EnrollmentRefund
        WHERE EnrollmentRefundId IN (SELECT EnrollmentRefundId FROM @EnrollmentRefundIds);

    IF OBJECT_ID(N'payment.PaymentCheckoutSession', N'U') IS NOT NULL
        DELETE FROM payment.PaymentCheckoutSession
        WHERE PaymentCheckoutSessionId IN (SELECT PaymentCheckoutSessionId FROM @CheckoutSessionIds);

    IF OBJECT_ID(N'payment.PaymentRefund', N'U') IS NOT NULL
        DELETE FROM payment.PaymentRefund
        WHERE PaymentId IN (SELECT PaymentId FROM @PaymentIds);

    IF OBJECT_ID(N'payment.PaymentPart', N'U') IS NOT NULL
        DELETE FROM payment.PaymentPart
        WHERE PaymentId IN (SELECT PaymentId FROM @PaymentIds);

    IF OBJECT_ID(N'payment.PaymentAllocation', N'U') IS NOT NULL
        DELETE FROM payment.PaymentAllocation
        WHERE PaymentId IN (SELECT PaymentId FROM @PaymentIds)
           OR BillId IN (SELECT BillId FROM @BillIds);

    IF OBJECT_ID(N'payment.Payment', N'U') IS NOT NULL
        DELETE FROM payment.Payment
        WHERE PaymentId IN (SELECT PaymentId FROM @PaymentIds);

    IF OBJECT_ID(N'fas.FASVoucherRedemption', N'U') IS NOT NULL
        DELETE FROM fas.FASVoucherRedemption
        WHERE BillId IN (SELECT BillId FROM @BillIds)
           OR CourseEnrollmentId IN (SELECT CourseEnrollmentId FROM @CourseEnrollmentIds)
           OR FASApplicationSchemeId IN (SELECT FASApplicationSchemeId FROM @FasApplicationSchemeIds)
           OR StudentPersonId IN (SELECT PersonId FROM @PersonIds);

    IF OBJECT_ID(N'fas.FASActiveScheme', N'U') IS NOT NULL
        DELETE FROM fas.FASActiveScheme
        WHERE StudentPersonId IN (SELECT PersonId FROM @PersonIds)
           OR FASApplicationSchemeId IN (SELECT FASApplicationSchemeId FROM @FasApplicationSchemeIds);

    IF OBJECT_ID(N'fas.FASStatusHistory', N'U') IS NOT NULL
        DELETE FROM fas.FASStatusHistory
        WHERE FASApplicationId IN (SELECT FASApplicationId FROM @FasApplicationIds)
           OR FASApplicationSchemeId IN (SELECT FASApplicationSchemeId FROM @FasApplicationSchemeIds);

    IF OBJECT_ID(N'fas.FASDeclaration', N'U') IS NOT NULL
        DELETE FROM fas.FASDeclaration
        WHERE FASApplicationId IN (SELECT FASApplicationId FROM @FasApplicationIds);

    IF OBJECT_ID(N'fas.FASDocument', N'U') IS NOT NULL
    BEGIN
        UPDATE fas.FASDocument
        SET ReplacedByDocumentId = NULL
        WHERE FASDocumentId IN (SELECT FASDocumentId FROM @FasDocumentIds)
           OR ReplacedByDocumentId IN (SELECT FASDocumentId FROM @FasDocumentIds);

        DELETE FROM fas.FASDocument
        WHERE FASDocumentId IN (SELECT FASDocumentId FROM @FasDocumentIds);
    END;

    IF OBJECT_ID(N'fas.FASSubsidy', N'U') IS NOT NULL
        DELETE FROM fas.FASSubsidy
        WHERE FASApplicationId IN (SELECT FASApplicationId FROM @FasApplicationIds);

    IF OBJECT_ID(N'fas.FASApplicationReviewDecision', N'U') IS NOT NULL
        DELETE FROM fas.FASApplicationReviewDecision
        WHERE FASApplicationId IN (SELECT FASApplicationId FROM @FasApplicationIds);

    IF OBJECT_ID(N'fas.FASApplicationScheme', N'U') IS NOT NULL
        DELETE FROM fas.FASApplicationScheme
        WHERE FASApplicationSchemeId IN (SELECT FASApplicationSchemeId FROM @FasApplicationSchemeIds);

    IF OBJECT_ID(N'fas.FASApplication', N'U') IS NOT NULL
        DELETE FROM fas.FASApplication
        WHERE FASApplicationId IN (SELECT FASApplicationId FROM @FasApplicationIds);

    IF OBJECT_ID(N'billing.BillDeferral', N'U') IS NOT NULL
        DELETE FROM billing.BillDeferral
        WHERE BillId IN (SELECT BillId FROM @BillIds)
           OR SourcePaymentId IN (SELECT PaymentId FROM @PaymentIds);

    IF OBJECT_ID(N'billing.BillingStatementItem', N'U') IS NOT NULL
        DELETE FROM billing.BillingStatementItem
        WHERE BillingStatementId IN (SELECT BillingStatementId FROM @BillingStatementIds)
           OR BillId IN (SELECT BillId FROM @BillIds);

    IF OBJECT_ID(N'billing.BillLine', N'U') IS NOT NULL
        DELETE FROM billing.BillLine
        WHERE BillId IN (SELECT BillId FROM @BillIds);

    IF OBJECT_ID(N'billing.Bill', N'U') IS NOT NULL
        DELETE FROM billing.Bill
        WHERE BillId IN (SELECT BillId FROM @BillIds);

    IF OBJECT_ID(N'billing.BillingStatement', N'U') IS NOT NULL
        DELETE FROM billing.BillingStatement
        WHERE BillingStatementId IN (SELECT BillingStatementId FROM @BillingStatementIds);

    IF OBJECT_ID(N'topup.DynamicTopUpContract', N'U') IS NOT NULL
        DELETE FROM topup.DynamicTopUpContract
        WHERE EducationAccountId IN (SELECT EducationAccountId FROM @EducationAccountIds)
           OR TopUpCampaignId IN (SELECT TopUpCampaignId FROM @TopUpCampaignIds);

    IF OBJECT_ID(N'topup.TopUpTransaction', N'U') IS NOT NULL
        DELETE FROM topup.TopUpTransaction
        WHERE EducationAccountId IN (SELECT EducationAccountId FROM @EducationAccountIds)
           OR TopUpRunId IN (SELECT TopUpRunId FROM @TopUpRunIds);

    IF OBJECT_ID(N'topup.TopUpCampaignRecipient', N'U') IS NOT NULL
        DELETE FROM topup.TopUpCampaignRecipient
        WHERE EducationAccountId IN (SELECT EducationAccountId FROM @EducationAccountIds);

    IF OBJECT_ID(N'topup.TopUpRun', N'U') IS NOT NULL
        DELETE FROM topup.TopUpRun
        WHERE TopUpRunId IN (SELECT TopUpRunId FROM @TopUpRunIds)
          AND NOT EXISTS
              (
                  SELECT 1
                  FROM topup.TopUpTransaction remaining
                  WHERE remaining.TopUpRunId = topup.TopUpRun.TopUpRunId
              );

    IF OBJECT_ID(N'course.CourseEnrollment', N'U') IS NOT NULL
        DELETE FROM course.CourseEnrollment
        WHERE CourseEnrollmentId IN (SELECT CourseEnrollmentId FROM @CourseEnrollmentIds);

    IF OBJECT_ID(N'account.EducationAccountLifecycleRunItem', N'U') IS NOT NULL
        DELETE FROM account.EducationAccountLifecycleRunItem
        WHERE PersonId IN (SELECT PersonId FROM @PersonIds)
           OR EducationAccountId IN (SELECT EducationAccountId FROM @EducationAccountIds);

    IF OBJECT_ID(N'account.EducationAccountLifecycleRun', N'U') IS NOT NULL
        DELETE FROM account.EducationAccountLifecycleRun
        WHERE EducationAccountLifecycleRunId IN (SELECT EducationAccountLifecycleRunId FROM @LifecycleRunIds)
          AND NOT EXISTS
              (
                  SELECT 1
                  FROM account.EducationAccountLifecycleRunItem remaining
                  WHERE remaining.EducationAccountLifecycleRunId = account.EducationAccountLifecycleRun.EducationAccountLifecycleRunId
              );

    IF OBJECT_ID(N'account.AccountSettlement', N'U') IS NOT NULL
        DELETE FROM account.AccountSettlement
        WHERE EducationAccountId IN (SELECT EducationAccountId FROM @EducationAccountIds);

    IF OBJECT_ID(N'account.AccountHold', N'U') IS NOT NULL
        DELETE FROM account.AccountHold
        WHERE EducationAccountId IN (SELECT EducationAccountId FROM @EducationAccountIds);

    IF OBJECT_ID(N'account.AccountTransaction', N'U') IS NOT NULL
        DELETE FROM account.AccountTransaction
        WHERE EducationAccountId IN (SELECT EducationAccountId FROM @EducationAccountIds);

    IF OBJECT_ID(N'account.SettlementPreference', N'U') IS NOT NULL
        DELETE FROM account.SettlementPreference
        WHERE EducationAccountId IN (SELECT EducationAccountId FROM @EducationAccountIds);

    IF OBJECT_ID(N'account.EducationAccount', N'U') IS NOT NULL
        DELETE FROM account.EducationAccount
        WHERE EducationAccountId IN (SELECT EducationAccountId FROM @EducationAccountIds);

    IF OBJECT_ID(N'iam.UserAccessScope', N'U') IS NOT NULL
        DELETE FROM iam.UserAccessScope
        WHERE UserAccountId IN (SELECT LoginAccountId FROM @LoginAccountIds);

    IF OBJECT_ID(N'iam.IdentityProvisioningRequest', N'U') IS NOT NULL
        DELETE FROM iam.IdentityProvisioningRequest
        WHERE PersonId IN (SELECT PersonId FROM @PersonIds)
           OR RequestedByUserAccountId IN (SELECT LoginAccountId FROM @LoginAccountIds);

    IF OBJECT_ID(N'iam.LoginMfaAuditEvent', N'U') IS NOT NULL
        DELETE FROM iam.LoginMfaAuditEvent
        WHERE LoginAccountId IN (SELECT LoginAccountId FROM @LoginAccountIds)
           OR PerformedByAccountId IN (SELECT LoginAccountId FROM @LoginAccountIds);

    IF OBJECT_ID(N'iam.LoginMfaChallenge', N'U') IS NOT NULL
        DELETE FROM iam.LoginMfaChallenge
        WHERE LoginAccountId IN (SELECT LoginAccountId FROM @LoginAccountIds);

    IF OBJECT_ID(N'iam.LoginMfaCredential', N'U') IS NOT NULL
        DELETE FROM iam.LoginMfaCredential
        WHERE LoginAccountId IN (SELECT LoginAccountId FROM @LoginAccountIds);

    IF OBJECT_ID(N'iam.LoginAccount', N'U') IS NOT NULL
        DELETE FROM iam.LoginAccount
        WHERE LoginAccountId IN (SELECT LoginAccountId FROM @LoginAccountIds);

    IF OBJECT_ID(N'audit.AuditLog', N'U') IS NOT NULL
        DELETE FROM audit.AuditLog
        WHERE
            (
                EntityTypeCode IN ('Person', 'Student')
                AND EntityId IN (SELECT PersonId FROM @PersonIds)
            )
            OR
            (
                EntityTypeCode IN ('UserAccount', 'LoginAccount')
                AND EntityId IN (SELECT LoginAccountId FROM @LoginAccountIds)
            )
            OR
            (
                EntityTypeCode = 'EducationAccount'
                AND EntityId IN (SELECT EducationAccountId FROM @EducationAccountIds)
            )
            OR PersonId IN (SELECT PersonId FROM @PersonIds)
            OR ActorLoginAccountId IN (SELECT LoginAccountId FROM @LoginAccountIds);

    IF OBJECT_ID(N'person.SchoolEnrollment', N'U') IS NOT NULL
        DELETE FROM person.SchoolEnrollment
        WHERE PersonId IN (SELECT PersonId FROM @PersonIds);

    IF OBJECT_ID(N'person.PersonIdentifier', N'U') IS NOT NULL
        DELETE FROM person.PersonIdentifier
        WHERE PersonId IN (SELECT PersonId FROM @PersonIds);

    IF OBJECT_ID(N'person.Person', N'U') IS NOT NULL
        DELETE FROM person.Person
        WHERE PersonId IN (SELECT PersonId FROM @PersonIds);

    SET @Sql = N'';
    SELECT @Sql = STRING_AGG(
        CAST(N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)
            + N' WITH CHECK CHECK CONSTRAINT ' + QUOTENAME(fk.name) + N';' AS nvarchar(max)),
        CHAR(10))
    FROM sys.foreign_keys fk
    JOIN @InitiallyEnabledForeignKeys enabled ON enabled.ObjectId = fk.object_id
    JOIN sys.tables t ON t.object_id = fk.parent_object_id
    JOIN sys.schemas s ON s.schema_id = t.schema_id;

    IF NULLIF(@Sql, N'') IS NOT NULL
        EXEC sys.sp_executesql @Sql;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT 'person.Person' AS Target, COUNT(*) AS RemainingRows
FROM person.Person
WHERE PersonId NOT BETWEEN @KeepPersonIdFrom AND @KeepPersonIdTo
UNION ALL
SELECT 'account.EducationAccount', COUNT(*)
FROM account.EducationAccount
WHERE PersonId NOT BETWEEN @KeepPersonIdFrom AND @KeepPersonIdTo
UNION ALL
SELECT 'iam.LoginAccount', COUNT(*)
FROM iam.LoginAccount
WHERE PersonId IS NOT NULL
  AND PersonId NOT BETWEEN @KeepPersonIdFrom AND @KeepPersonIdTo
UNION ALL
SELECT 'person.PersonIdentifier', COUNT(*)
FROM person.PersonIdentifier
WHERE PersonId NOT BETWEEN @KeepPersonIdFrom AND @KeepPersonIdTo
UNION ALL
SELECT 'person.SchoolEnrollment', COUNT(*)
FROM person.SchoolEnrollment
WHERE PersonId NOT BETWEEN @KeepPersonIdFrom AND @KeepPersonIdTo;

PRINT 'Non-preserved people cleanup completed successfully.';
