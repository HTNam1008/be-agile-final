/*
    Seeds baseline course fee components.

    Domain values:
      ComponentTypeCode: BASE, ADDON, TAX
      CalculationTypeCode: FIXED, PERCENTAGE

    This script is idempotent. Existing rows with the same ComponentCode are
    updated; missing rows are inserted.
*/

SET XACT_ABORT ON;

BEGIN TRANSACTION;

MERGE [course].[FeeComponent] AS target
USING
(
    VALUES
        (N'TUITION',  N'Tuition fee',             'BASE',     'FIXED',      CAST(0 AS bit), CAST(1 AS bit)),
        (N'MATERIAL', N'Material fee',            'ADDON',    'FIXED',      CAST(0 AS bit), CAST(1 AS bit)),
        (N'TAX',      N'Goods and services tax',  'TAX',      'PERCENTAGE', CAST(1 AS bit), CAST(1 AS bit))
) AS source
(
    [ComponentCode],
    [ComponentName],
    [ComponentTypeCode],
    [CalculationTypeCode],
    [IsTaxComponent],
    [IsActive]
)
ON target.[ComponentCode] = source.[ComponentCode]
WHEN MATCHED THEN
    UPDATE SET
        target.[ComponentName] = source.[ComponentName],
        target.[ComponentTypeCode] = source.[ComponentTypeCode],
        target.[CalculationTypeCode] = source.[CalculationTypeCode],
        target.[IsTaxComponent] = source.[IsTaxComponent],
        target.[IsActive] = source.[IsActive]
WHEN NOT MATCHED BY TARGET THEN
    INSERT
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
        source.[ComponentCode],
        source.[ComponentName],
        source.[ComponentTypeCode],
        source.[CalculationTypeCode],
        source.[IsTaxComponent],
        source.[IsActive]
    );

COMMIT TRANSACTION;

SELECT
    [FeeComponentId],
    [ComponentCode],
    [ComponentName],
    [ComponentTypeCode],
    [CalculationTypeCode],
    [IsTaxComponent],
    [IsActive]
FROM [course].[FeeComponent]
WHERE [ComponentCode] IN (N'TUITION', N'MATERIAL', N'TAX')
ORDER BY [ComponentCode];
