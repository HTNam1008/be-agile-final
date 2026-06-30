/*
    Seed data for testing:
      1. Admin enrollment
      2. Student self enrollment

    Domain prerequisites:
      - Person 2001 exists.
      - Person 2001 has an ACTIVE SchoolEnrollment at Organization 2.
      - Each course belongs to Organization 2.
      - Each course has at least one active CourseFee whose FeeComponent is active.

    This script DOES NOT delete or modify CourseEnrollment, Bill, or BillLine data.
    It is safe to rerun, but an API call already completed for the same
    person/course will correctly return COURSE.ENROLLMENT_DUPLICATE.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @PersonId bigint = 2001;
    DECLARE @OrganizationId bigint = 2;
    DECLARE @AdminCourseCode nvarchar(50) = N'API-ADMIN-ENROLL-2026';
    DECLARE @SelfCourseCode nvarchar(50) = N'API-SELF-ENROLL-2026';
    DECLARE @FeeComponentCode nvarchar(50) = N'API-TEST-TUITION';
    DECLARE @Now datetime2 = SYSUTCDATETIME();
    DECLARE @Today date = CAST(@Now AS date);

    IF NOT EXISTS (
        SELECT 1
        FROM [person].[Person]
        WHERE [PersonId] = @PersonId
    )
    BEGIN
        THROW 51000, 'Required demo PersonId 2001 does not exist. Apply the demo seed migrations first.', 1;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM [org].[Organization]
        WHERE [OrganizationId] = @OrganizationId
          AND [OrganizationStatusCode] = 'ACTIVE'
    )
    BEGIN
        THROW 51001, 'Required active OrganizationId 2 does not exist. Apply the demo seed migrations first.', 1;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM [person].[SchoolEnrollment]
        WHERE [PersonId] = @PersonId
          AND [OrganizationId] = @OrganizationId
          AND [SchoolingStatusCode] = 'ACTIVE'
          AND [StartDate] <= @Today
          AND ([EndDate] IS NULL OR [EndDate] >= @Today)
    )
    BEGIN
        THROW 51002, 'PersonId 2001 needs an active SchoolEnrollment at OrganizationId 2.', 1;
    END;

    DECLARE @FeeComponentId bigint;

    SELECT @FeeComponentId = [FeeComponentId]
    FROM [course].[FeeComponent]
    WHERE [ComponentCode] = @FeeComponentCode;

    IF @FeeComponentId IS NULL
    BEGIN
        INSERT INTO [course].[FeeComponent]
        (
            [ComponentCode],
            [ComponentName],
            [ComponentTypeCode],
            [CalculationTypeCode],
            [IsTaxComponent],
            [IsActive]
        )
        VALUES
        (
            @FeeComponentCode,
            N'API Test Tuition Fee',
            'BASE',
            'FIXED',
            0,
            1
        );

        SET @FeeComponentId = SCOPE_IDENTITY();
    END;
    ELSE
    BEGIN
        UPDATE [course].[FeeComponent]
        SET [ComponentName] = N'API Test Tuition Fee',
            [ComponentTypeCode] = 'BASE',
            [CalculationTypeCode] = 'FIXED',
            [IsTaxComponent] = 0,
            [IsActive] = 1
        WHERE [FeeComponentId] = @FeeComponentId;
    END;

    DECLARE @AdminCourseId bigint;
    DECLARE @SelfCourseId bigint;

    SELECT @AdminCourseId = [CourseId]
    FROM [course].[Course]
    WHERE [CourseCode] = @AdminCourseCode;

    IF @AdminCourseId IS NULL
    BEGIN
        INSERT INTO [course].[Course]
        (
            [OrganizationId],
            [CourseCode],
            [CourseName],
            [Description],
            [StartDate],
            [EndDate],
            [EnrollmentOpenAt],
            [EnrollmentCloseAt],
            [CourseStatusCode],
            [CreatedByLoginAccountId],
            [UpdatedByLoginAccountId],
            [UpdatedAt],
            [DisabledByLoginAccountId],
            [DisabledAt]
        )
        VALUES
        (
            @OrganizationId,
            @AdminCourseCode,
            N'API Test - Admin Enrollment',
            N'Course reserved for testing POST admin enrollment.',
            DATEADD(day, 1, @Today),
            DATEADD(month, 3, @Today),
            DATEADD(day, -1, @Now),
            DATEADD(month, 1, @Now),
            'PUBLISHED',
            1001,
            1001,
            @Now,
            NULL,
            NULL
        );

        SET @AdminCourseId = SCOPE_IDENTITY();
    END;
    ELSE
    BEGIN
        UPDATE [course].[Course]
        SET [OrganizationId] = @OrganizationId,
            [CourseName] = N'API Test - Admin Enrollment',
            [Description] = N'Course reserved for testing POST admin enrollment.',
            [StartDate] = DATEADD(day, 1, @Today),
            [EndDate] = DATEADD(month, 3, @Today),
            [EnrollmentOpenAt] = DATEADD(day, -1, @Now),
            [EnrollmentCloseAt] = DATEADD(month, 1, @Now),
            [CourseStatusCode] = 'PUBLISHED',
            [UpdatedByLoginAccountId] = 1001,
            [UpdatedAt] = @Now,
            [DisabledByLoginAccountId] = NULL,
            [DisabledAt] = NULL
        WHERE [CourseId] = @AdminCourseId;
    END;

    SELECT @SelfCourseId = [CourseId]
    FROM [course].[Course]
    WHERE [CourseCode] = @SelfCourseCode;

    IF @SelfCourseId IS NULL
    BEGIN
        INSERT INTO [course].[Course]
        (
            [OrganizationId],
            [CourseCode],
            [CourseName],
            [Description],
            [StartDate],
            [EndDate],
            [EnrollmentOpenAt],
            [EnrollmentCloseAt],
            [CourseStatusCode],
            [CreatedByLoginAccountId],
            [UpdatedByLoginAccountId],
            [UpdatedAt],
            [DisabledByLoginAccountId],
            [DisabledAt]
        )
        VALUES
        (
            @OrganizationId,
            @SelfCourseCode,
            N'API Test - Self Enrollment',
            N'Course reserved for testing POST student self enrollment.',
            DATEADD(day, 1, @Today),
            DATEADD(month, 3, @Today),
            DATEADD(day, -1, @Now),
            DATEADD(month, 1, @Now),
            'PUBLISHED',
            1001,
            1001,
            @Now,
            NULL,
            NULL
        );

        SET @SelfCourseId = SCOPE_IDENTITY();
    END;
    ELSE
    BEGIN
        UPDATE [course].[Course]
        SET [OrganizationId] = @OrganizationId,
            [CourseName] = N'API Test - Self Enrollment',
            [Description] = N'Course reserved for testing POST student self enrollment.',
            [StartDate] = DATEADD(day, 1, @Today),
            [EndDate] = DATEADD(month, 3, @Today),
            [EnrollmentOpenAt] = DATEADD(day, -1, @Now),
            [EnrollmentCloseAt] = DATEADD(month, 1, @Now),
            [CourseStatusCode] = 'PUBLISHED',
            [UpdatedByLoginAccountId] = 1001,
            [UpdatedAt] = @Now,
            [DisabledByLoginAccountId] = NULL,
            [DisabledAt] = NULL
        WHERE [CourseId] = @SelfCourseId;
    END;

    MERGE [course].[CourseFee] AS target
    USING
    (
        SELECT @AdminCourseId AS [CourseId], @FeeComponentId AS [FeeComponentId], CAST(125.00 AS decimal(19,4)) AS [FeeValue]
        UNION ALL
        SELECT @SelfCourseId, @FeeComponentId, CAST(150.00 AS decimal(19,4))
    ) AS source
    ON target.[CourseId] = source.[CourseId]
       AND target.[FeeComponentId] = source.[FeeComponentId]
    WHEN MATCHED THEN
        UPDATE SET
            [FeeValue] = source.[FeeValue],
            [SequenceNumber] = 1,
            [IsActive] = 1
    WHEN NOT MATCHED THEN
        INSERT ([CourseId], [FeeComponentId], [FeeValue], [SequenceNumber], [IsActive])
        VALUES (source.[CourseId], source.[FeeComponentId], source.[FeeValue], 1, 1);

    COMMIT TRANSACTION;

    SELECT
        @PersonId AS [TestPersonId],
        @OrganizationId AS [OrganizationId],
        @AdminCourseId AS [AdminEnrollmentCourseId],
        @SelfCourseId AS [SelfEnrollmentCourseId],
        @FeeComponentId AS [FeeComponentId];
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
