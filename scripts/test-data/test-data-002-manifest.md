# TEST-DATA-002 Manual Test Data Manifest

## Step 0 Findings

- UM-015 bulk import reads the first worksheet and uses exact, case-insensitive headers: `SchoolName`, `OrganizationId`, `IdentityNumber`, `FullName`, `DateOfBirth`, `NationalityCode`, `CitizenshipStatusCode`, `StudentNumber`, `AcademicYear`, `LevelCode`, `ClassCode`, `StartDate`, `Email`, `Mobile`, `Address`.
- Date cells are real Excel date values in the fixture and display as `yyyy-mm-dd`. The parser also accepts parseable date text, but this fixture uses typed dates.
- Happy-path workbook rows resolve the school by `SchoolName = National University of Singapore` and leave `OrganizationId` blank. The final workbook row intentionally supplies both `OrganizationId = 2` and `SchoolName = QA TEST AUTO002 School B` to exercise the school-identifier conflict path.
- `PersonStatusCode` is not part of the bulk-import row shape. The non-ACTIVE person case is direct-seeded as `DISABLED`.
- Demo config in `src/Hosts/Moe.StudentFinance.Api/appsettings.json` is `EducationAccountLifecycle:Enabled = true`, `RunAtUtc = "02:00"`. For manual testing, use AUTO-003 `run-now`; do not wait for the schedule.
- The fixture was generated for `today = 2026-06-24` (UTC/system date at generation time). Boundary DOBs are relative to that date; regenerate the workbook if testing much later.

## Files

- Excel fixture: `scripts/test-data/test-data-002-auto-lifecycle-bulk-import.xlsx`
- Self-cleaning seed script: `scripts/test-data/test-data-002-seed.sql`
- Cleanup-only script: `scripts/test-data/test-data-002-cleanup.sql`

File-count note: TEST-DATA-001 uses separate `sprint2-part1-seed.sql` and `sprint2-part1-cleanup.sql` files. TEST-DATA-002 intentionally follows the newer prompt clarification instead: one seed SQL file that deletes this fixture's prior rows before inserting the baseline direct-seed rows.

## Run Seed

Use the local demo database from `appsettings.json`:

```powershell
sqlcmd -S localhost -d StudentFinance -E -C -i scripts/test-data/test-data-002-seed.sql
```

The seed is idempotent and creates only:

- `QA_TEST_AUTO002_SCHOOL_B` for the school-conflict import row.
- Four direct-seeded people for pre-existing account/non-ACTIVE states.
- Three direct-seeded education accounts:
  - `QA-AUTO002-EA-101`: age 25, active account, should not get a duplicate.
  - `QA-AUTO002-EA-102`: age 31, active account, should close on `run-now`.
  - `QA-AUTO002-EA-103`: age 31, already closed, should remain no-op.

At the start of each run, the seed script deletes prior TEST-DATA-002 rows by the fixture prefixes/NRICs:

- `QA TEST AUTO002%`
- `QA_TEST_AUTO002_%`
- `QA-AUTO002-EA-%`
- `QA_TEST_AUTO002_SCHOOL_B`
- Excel NRICs `S912001A` through `S912009J`, plus TEST-DATA-003 MockPass-aligned NRICs `S7000051E` through `S7000060P`

## Swagger Flow

1. Get an admin token if needed:

```http
GET /dev/admin-token
```

2. Bulk import the Excel fixture:

```http
POST /api/admin/v1/students/bulk-import
Content-Type: multipart/form-data
file = scripts/test-data/test-data-002-auto-lifecycle-bulk-import.xlsx
```

Expected response:

| Excel row | Purpose | Expected |
| ---: | --- | --- |
| 2 | Age 22 citizen, `National University of Singapore` | `Succeeded` |
| 3 | Exact age 16 citizen, `National University of Singapore` | `Succeeded` |
| 4 | Exact age 30 citizen, `National University of Singapore` | `Succeeded` |
| 5 | Age 15 citizen, `National University of Singapore` | `Succeeded` |
| 6 | Age 31 citizen, no account, `National University of Singapore` | `Succeeded` |
| 7 | Age 22 `VALID_PASS_HOLDER`, `National University of Singapore` | `Succeeded` |
| 8 | Duplicate-in-file NRIC: row 8 `IdentityNumber = S912001A`, same as row 2 | `Failed`, `IDENTITY.STUDENT_IDENTITY_ALREADY_EXISTS` |
| 9 | Blank required field: `FullName` is empty | `Failed`, `BULK_IMPORT.ROW_VALIDATION_FAILED`; message: `'Full Name' must not be empty.` |
| 10 | `OrganizationId = 2` plus `SchoolName = QA TEST AUTO002 School B` | `Failed`, `IDENTITY.SCHOOL_IDENTIFIERS_CONFLICT` |
| 11 | TEST-DATA-003 MockPass-aligned `BACHELOR` row, `IdentityNumber = S7000051E` | `Succeeded` |
| 12 | TEST-DATA-003 MockPass-aligned `MASTER` row with blank `CitizenshipStatusCode`, `IdentityNumber = S7000052F` | `Succeeded` |
| 13 | TEST-DATA-003 MockPass-aligned `PHD` row, `IdentityNumber = S7000053G` | `Succeeded` |
| 14 | TEST-DATA-003 MockPass-aligned `BACHELOR` row with blank `CitizenshipStatusCode`, `IdentityNumber = S7000054H` | `Succeeded` |
| 15 | TEST-DATA-003 MockPass-aligned `BACHELOR` row with `VALID_PASS_HOLDER`, `IdentityNumber = S7000055J` | `Succeeded` |
| 16 | TEST-DATA-003 MockPass-aligned future `StartDate` row, `IdentityNumber = S7000056K` | `Succeeded` |
| 17 | TEST-DATA-003 MockPass-aligned organization-only school resolution row, `IdentityNumber = S7000057L` | `Succeeded` |
| 18 | TEST-DATA-003 MockPass-aligned school-name-only row with blank class/citizenship, `IdentityNumber = S7000058M` | `Succeeded` |
| 19 | TEST-DATA-003 MockPass-aligned `PHD` row with `VALID_PASS_HOLDER`, `IdentityNumber = S7000059N` | `Succeeded` |
| 20 | TEST-DATA-003 MockPass-aligned organization-only secondary row with blank `CitizenshipStatusCode`, `IdentityNumber = S7000060P` | `Succeeded` |

Expected bulk totals after TEST-DATA-003 expansion: `totalRows = 19`, `succeededCount = 16`, `failedCount = 3`.

TEST-DATA-003 adds 10 valid MockPass-aligned rows to the existing 9-row TEST-DATA-002 workbook. The expanded fixture therefore has 19 data rows, not 20 data rows; Excel row 20 is the final data row because Excel row 1 is the header. The three original intentionally invalid rows remain rows 8-10.

Verification note, generated on 2026-06-24: the workbook was posted once to the real bulk-import HTTP endpoint through a fresh integration host/fake-auth harness, not the full xUnit suite. Observed response was `HTTP 200`, `totalRows = 9`, `succeededCount = 6`, `failedCount = 3`. Row 8 was verified as a duplicate-in-file case because row 2 and row 8 both carry `IdentityNumber = S912001A`; the fresh harness did not contain prior `S91200x` fixture identities before this request. Row 9 failed because `FullName` is blank, with response message `'Full Name' must not be empty.` Row 10 failed with `IDENTITY.SCHOOL_IDENTIFIERS_CONFLICT`.

Lifecycle count note: the first-run lifecycle counts below were verified against the pre-TEST-DATA-003 workbook (`totalRows = 9`, `succeededCount = 6`, `failedCount = 3`). After the TEST-DATA-003 expansion, re-run the seed/import/lifecycle flow against a freshly seeded database before treating the lifecycle counts as final.

3. Trigger lifecycle immediately:

```http
POST /api/admin/v1/education-account-lifecycle/run-now
```

Expected first-run response after seed + import:

```json
{
  "openedCount": 3,
  "closedCount": 1
}
```

Why:

- AUTO-001 opens accounts for imported age 22, exact age 16, and exact age 30 citizens.
- AUTO-001 skips imported age 15, imported age 31 with no account, imported non-citizen, and direct-seeded `DISABLED` person.
- AUTO-001 sees the direct-seeded age 25 person as eligible but does not create a duplicate because an active account already exists.
- AUTO-002 closes only direct-seeded `QA-AUTO002-EA-102` on the first run. The exact-age-30 imported row is opened after the close pass because lifecycle runs close first, then open; it will close on a later run.
- Direct-seeded `QA-AUTO002-EA-103` is already closed and remains a no-op with no duplicate close audit.

## Expected Student Filters After First Run

Use `GET /api/admin/v1/students` and search for `QA TEST AUTO002`.

| Fixture row | Expected account state |
| --- | --- |
| Imported age 22 citizen | `ACTIVE` |
| Imported exact age 16 citizen | `ACTIVE` |
| Imported exact age 30 citizen | `ACTIVE` after first run; time-sensitive edge case |
| Imported age 15 citizen | `NO_ACCOUNT` |
| Imported age 31 citizen, no account | `NO_ACCOUNT` |
| Imported non-citizen | `NO_ACCOUNT` |
| Direct age 25 with existing active account | `ACTIVE`, still one account |
| Direct age 31 with existing active account | `CLOSED`, `ClosingReasonCode = AUTO_AGE_LIMIT` |
| Direct age 31 already closed account | `CLOSED`, unchanged/no-op |
| Direct disabled eligible-by-age/citizenship person | `NO_ACCOUNT` |

Suggested filters:

- `accountStatus=Active` should include the three opened import rows plus the direct age-25 existing-account row.
- `accountStatus=NoAccount` should include age 15, age 31 without account, non-citizen, and disabled direct seed.
- `accountStatus=Closed` should include both direct age-31 account rows after first run.

## Cleanup / Reset

To remove TEST-DATA-002 rows without recreating the baseline:

```powershell
sqlcmd -S localhost -d StudentFinance -E -C -i scripts/test-data/test-data-002-cleanup.sql
```

To reset and recreate the direct-seeded baseline, run the same self-cleaning seed script again:

```powershell
sqlcmd -S localhost -d StudentFinance -E -C -i scripts/test-data/test-data-002-seed.sql
```

This removes any prior TEST-DATA-002 import/lifecycle rows and recreates the direct-seeded baseline. After that, re-upload the workbook to repeat the Swagger demo from a clean state.

