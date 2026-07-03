# TEST-DATA-003-006 MockPass Import Demo Manifest

## Purpose

These four independent bulk-import fixtures are demo-only files for importing valid students and then logging in through mockpass as the exact imported students. Each workbook has 17 importable rows backed by unique mockpass accounts in `E:/Final Agile/mockpass/static/mock-identities.json`, plus 3 intentional failed-import rows.

They are independent from the AUTO lifecycle scenario in `test-data-002-auto-lifecycle-bulk-import.xlsx`, but follow the same `17 succeeded / 3 failed` import shape.

## Schema

- Sheet: `BulkImport`
- Data rows per file: `20`
- Header: `SchoolName`, `OrganizationId`, `IdentityNumber`, `FullName`, `DateOfBirth`, `NationalityCode`, `CitizenshipStatusCode`, `StudentNumber`, `AcademicYear`, `LevelCode`, `ClassCode`, `StartDate`, `Email`, `Mobile`, `Address`
- Date columns use typed Excel dates with `yyyy-mm-dd` formatting.
- `IdentityNumber`, `FullName`, `DateOfBirth`, `StudentNumber`, `Email`, `Mobile`, and `Address` mirror the matching records in `mock-identities.json`.
- `SchoolName` is blank and `OrganizationId` is `2` for every row so the fixture imports against the seeded school used by the backend test host.
- Rows 19, 20, and 21 intentionally fail import: invalid identity format, missing full name, and start date before date of birth.

## Files

| File | Data rows | NRIC range |
| --- | ---: | --- |
| `scripts/test-data/test-data-003-mockpass-import-demo.xlsx` | 20 | `S7000061Z` through `S7000080F` |
| `scripts/test-data/test-data-004-mockpass-import-demo.xlsx` | 20 | `S7000081D` through `S7000100D` |
| `scripts/test-data/test-data-005-mockpass-import-demo.xlsx` | 20 | `S7000101B` through `S7000120I` |
| `scripts/test-data/test-data-006-mockpass-import-demo.xlsx` | 20 | `S7000121G` through `S7000140C` |

Cleanup script: `scripts/test-data/test-data-003-006-mockpass-import-demo-cleanup.sql`.

## MockPass Accounts

- Baseline before these fixtures: 60 students, ending at `S7000060P`.
- Referenced by these fixtures: 80 mockpass students, `S7000061Z` through `S7000140C`.
- Importable from these fixtures: 68 students; 12 rows are intentional failed-import rows.
- Expected mockpass total after this manifest: 140 students.
- `singpassId` equals `nric` for every added account.
- UUID derivation: `uuid5(NAMESPACE_URL, f"mockpass-import-demo:{nric}")`.
- New sequential IDs continue from TEST-DATA-003: `loginAccountId`/`userAccessScopeId` 1063-1142, `personId` 2061-2140, `enrollmentId` 3061-3140, `educationAccountId` 4061-4140.

## NRIC Lists

### test-data-003-mockpass-import-demo.xlsx

`S7000061Z`, `S7000062H`, `S7000063F`, `S7000064D`, `S7000065B`, `S7000066J`, `S7000067I`, `S7000068G`, `S7000069E`, `S7000070I`, `S7000071G`, `S7000072E`, `S7000073C`, `S7000074A`, `S7000075Z`, `S7000076H`, `S7000077F`, `S7000078D`, `S7000079B`, `S7000080F`

### test-data-004-mockpass-import-demo.xlsx

`S7000081D`, `S7000082B`, `S7000083J`, `S7000084I`, `S7000085G`, `S7000086E`, `S7000087C`, `S7000088A`, `S7000089Z`, `S7000090C`, `S7000091A`, `S7000092Z`, `S7000093H`, `S7000094F`, `S7000095D`, `S7000096B`, `S7000097J`, `S7000098I`, `S7000099G`, `S7000100D`

### test-data-005-mockpass-import-demo.xlsx

`S7000101B`, `S7000102J`, `S7000103I`, `S7000104G`, `S7000105E`, `S7000106C`, `S7000107A`, `S7000108Z`, `S7000109H`, `S7000110A`, `S7000111Z`, `S7000112H`, `S7000113F`, `S7000114D`, `S7000115B`, `S7000116J`, `S7000117I`, `S7000118G`, `S7000119E`, `S7000120I`

### test-data-006-mockpass-import-demo.xlsx

`S7000121G`, `S7000122E`, `S7000123C`, `S7000124A`, `S7000125Z`, `S7000126H`, `S7000127F`, `S7000128D`, `S7000129B`, `S7000130F`, `S7000131D`, `S7000132B`, `S7000133J`, `S7000134I`, `S7000135G`, `S7000136E`, `S7000137C`, `S7000138A`, `S7000139Z`, `S7000140C`

## Expected Import Result

For each workbook, from a clean database without those identities already imported:

```text
totalRows = 20
succeededCount = 17
failedCount = 3
```

No rows use `UNI_Y1` or any other `UNI_Y*` level code.

## Cleanup / Reset

To remove students imported from these four workbooks, including any JIT-provisioned Singpass login accounts created after mockpass login, run:

```powershell
sqlcmd -S localhost -d StudentFinance -E -C -i scripts/test-data/test-data-003-006-mockpass-import-demo-cleanup.sql
```

