/*
    Repairs drifted development databases where the TopUpRun model is newer than
    the physical table but the historical migrations cannot be replayed safely.

    Clean databases should use EF Core migrations instead.
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'topup.TopUpRun', N'U') IS NULL
    THROW 50001, 'Table topup.TopUpRun does not exist. Apply the base migrations first.', 1;

IF COL_LENGTH(N'topup.TopUpRun', N'Note') IS NULL
BEGIN
    ALTER TABLE [topup].[TopUpRun]
        ADD [Note] nvarchar(500) NULL;
END;

IF COL_LENGTH(N'topup.TopUpRun', N'TotalSkipped') IS NULL
BEGIN
    ALTER TABLE [topup].[TopUpRun]
        ADD [TotalSkipped] int NOT NULL
            CONSTRAINT [DF_TopUpRun_TotalSkipped] DEFAULT (0) WITH VALUES;
END;

COMMIT TRANSACTION;

SELECT
    columnRow.name AS ColumnName,
    typeRow.name AS DataType,
    columnRow.max_length AS MaxLength,
    columnRow.is_nullable AS IsNullable
FROM sys.columns AS columnRow
INNER JOIN sys.types AS typeRow
    ON columnRow.user_type_id = typeRow.user_type_id
WHERE columnRow.object_id = OBJECT_ID(N'topup.TopUpRun')
  AND columnRow.name IN (N'Note', N'TotalSkipped')
ORDER BY columnRow.name;
