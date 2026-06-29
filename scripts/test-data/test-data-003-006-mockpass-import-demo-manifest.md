# TEST-DATA-003-006 MockPass Import Demo Manifest

## Purpose

These four independent bulk-import fixtures are demo-only files for importing valid students and then logging in through mockpass as the exact imported students. Every row is intentionally valid and backed by a unique mockpass account in `E:/Final Agile/mockpass/static/mock-identities.json`.

They are independent from the AUTO lifecycle scenario in `test-data-002-auto-lifecycle-bulk-import.xlsx`; no intentionally invalid rows are included here.

## Schema

- Sheet: `BulkImport`
- Data rows per file: `20`
- Header: `SchoolName`, `OrganizationId`, `IdentityNumber`, `FullName`, `DateOfBirth`, `NationalityCode`, `CitizenshipStatusCode`, `StudentNumber`, `AcademicYear`, `LevelCode`, `ClassCode`, `StartDate`, `Email`, `Mobile`, `Address`
- Date columns use typed Excel dates with `yyyy-mm-dd` formatting.

## Files

| File | Data rows | NRIC range |
| --- | ---: | --- |
| `scripts/test-data/test-data-003-mockpass-import-demo.xlsx` | 20 | `S7000061Q` through `S7000080L` |
| `scripts/test-data/test-data-004-mockpass-import-demo.xlsx` | 20 | `S7000081M` through `S7000100H` |
| `scripts/test-data/test-data-005-mockpass-import-demo.xlsx` | 20 | `S7000101J` through `S7000120E` |
| `scripts/test-data/test-data-006-mockpass-import-demo.xlsx` | 20 | `S7000121F` through `S7000140B` |

Cleanup script: `scripts/test-data/test-data-003-006-mockpass-import-demo-cleanup.sql`.

## MockPass Accounts

- Baseline before these fixtures: 60 students, ending at `S7000060P`.
- Added by these fixtures: 80 students, `S7000061Q` through `S7000140B`.
- Expected mockpass total after this manifest: 140 students.
- `singpassId` equals `nric` for every added account.
- UUID derivation: `uuid5(NAMESPACE_URL, f"mockpass-import-demo:{nric}")`.
- New sequential IDs continue from TEST-DATA-003: `loginAccountId`/`userAccessScopeId` 1063-1142, `personId` 2061-2140, `enrollmentId` 3061-3140, `educationAccountId` 4061-4140.

## NRIC Lists

### test-data-003-mockpass-import-demo.xlsx

`S7000061Q`, `S7000062R`, `S7000063T`, `S7000064U`, `S7000065V`, `S7000066W`, `S7000067X`, `S7000068Y`, `S7000069Z`, `S7000070A`, `S7000071B`, `S7000072C`, `S7000073D`, `S7000074E`, `S7000075F`, `S7000076G`, `S7000077H`, `S7000078J`, `S7000079K`, `S7000080L`

### test-data-004-mockpass-import-demo.xlsx

`S7000081M`, `S7000082N`, `S7000083P`, `S7000084Q`, `S7000085R`, `S7000086T`, `S7000087U`, `S7000088V`, `S7000089W`, `S7000090X`, `S7000091Y`, `S7000092Z`, `S7000093A`, `S7000094B`, `S7000095C`, `S7000096D`, `S7000097E`, `S7000098F`, `S7000099G`, `S7000100H`

### test-data-005-mockpass-import-demo.xlsx

`S7000101J`, `S7000102K`, `S7000103L`, `S7000104M`, `S7000105N`, `S7000106P`, `S7000107Q`, `S7000108R`, `S7000109T`, `S7000110U`, `S7000111V`, `S7000112W`, `S7000113X`, `S7000114Y`, `S7000115Z`, `S7000116A`, `S7000117B`, `S7000118C`, `S7000119D`, `S7000120E`

### test-data-006-mockpass-import-demo.xlsx

`S7000121F`, `S7000122G`, `S7000123H`, `S7000124J`, `S7000125K`, `S7000126L`, `S7000127M`, `S7000128N`, `S7000129P`, `S7000130Q`, `S7000131R`, `S7000132T`, `S7000133U`, `S7000134V`, `S7000135W`, `S7000136X`, `S7000137Y`, `S7000138Z`, `S7000139A`, `S7000140B`

## Expected Import Result

For each workbook, from a clean database without those identities already imported:

```text
totalRows = 20
succeededCount = 20
failedCount = 0
```

No rows use `UNI_Y1` or any other `UNI_Y*` level code.

## Cleanup / Reset

To remove students imported from these four workbooks, including any JIT-provisioned Singpass login accounts created after mockpass login, run:

```powershell
sqlcmd -S localhost -d StudentFinance -E -C -i scripts/test-data/test-data-003-006-mockpass-import-demo-cleanup.sql
```
