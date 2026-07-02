/*
    Creates or repairs the system-managed GST 9% fee component.
    Safe to run repeatedly after the FeeComponent table and its current columns exist.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'course.FeeComponent', N'U') IS NULL
BEGIN
    THROW 51000, 'Table course.FeeComponent does not exist. Apply the schema migrations first.', 1;
END;

IF COL_LENGTH(N'course.FeeComponent', N'DefaultValue') IS NULL
   OR COL_LENGTH(N'course.FeeComponent', N'IsSystemManaged') IS NULL
BEGIN
    THROW 51001, 'FeeComponent schema is outdated. DefaultValue or IsSystemManaged is missing.', 1;
END;

BEGIN TRANSACTION;

MERGE [course].[FeeComponent] WITH (HOLDLOCK) AS target
USING
(
    VALUES
    (
        N'GST',
        N'GST 9%',
        'TAX',
        'PERCENTAGE',
        CAST(1 AS bit),
        CAST(1 AS bit),
        CAST(9.0000 AS decimal(19,4)),
        CAST(1 AS bit)
    )
) AS source
(
    [ComponentCode],
    [ComponentName],
    [ComponentTypeCode],
    [CalculationTypeCode],
    [IsTaxComponent],
    [IsActive],
    [DefaultValue],
    [IsSystemManaged]
)
ON target.[ComponentCode] = source.[ComponentCode]
WHEN MATCHED THEN
    UPDATE SET
        target.[ComponentName] = source.[ComponentName],
        target.[ComponentTypeCode] = source.[ComponentTypeCode],
        target.[CalculationTypeCode] = source.[CalculationTypeCode],
        target.[IsTaxComponent] = source.[IsTaxComponent],
        target.[IsActive] = source.[IsActive],
        target.[DefaultValue] = source.[DefaultValue],
        target.[IsSystemManaged] = source.[IsSystemManaged]
WHEN NOT MATCHED BY TARGET THEN
    INSERT
    (
        [ComponentCode],
        [ComponentName],
        [ComponentTypeCode],
        [CalculationTypeCode],
        [IsTaxComponent],
        [IsActive],
        [DefaultValue],
        [IsSystemManaged]
    )
    VALUES
    (
        source.[ComponentCode],
        source.[ComponentName],
        source.[ComponentTypeCode],
        source.[CalculationTypeCode],
        source.[IsTaxComponent],
        source.[IsActive],
        source.[DefaultValue],
        source.[IsSystemManaged]
    );

COMMIT TRANSACTION;

SELECT
    [FeeComponentId],
    [ComponentCode],
    [ComponentName],
    [ComponentTypeCode],
    [CalculationTypeCode],
    [IsTaxComponent],
    [DefaultValue],
    [IsSystemManaged],
    [IsActive]
FROM [course].[FeeComponent]
WHERE [ComponentCode] = N'GST';
