# Sprint 2 Part 1 Manual Test Data Manifest

## Baseline Check

Checked on local `StudentFinance` via `sqlcmd -S localhost -d StudentFinance -E -C`.

| Table | Baseline Count |
| --- | ---: |
| `org.Organization` | 2 |
| `person.Person` | 1 |
| `account.EducationAccount` | 1 |
| `iam.LoginAccount` | 3 |

Existing organizations:

| OrganizationId | Code | Purpose |
| ---: | --- | --- |
| 1 | `MOE_HQ` | Existing HQ |
| 2 | `DEMO_SCHOOL` | School A / in-scope school |
| 900002 | `QA_TEST_SCHOOL_B` | Created by seed if missing; School B / out-of-scope school |

## Admin / Token Notes

`GET /dev/admin-token` is hardcoded and does not support role/org/permission overrides. It mints an HQ-style admin token only.

School Admin must be created outside these scripts via Azure AD + `POST /api/admin/v1/admin-users`.

Suggested request body:

```json
{
  "email": "qa.school.a.admin@moe.local",
  "displayName": "QA School A Admin",
  "mailNickname": "qa-school-a-admin",
  "temporaryPassword": "TempPass#2026!",
  "initialOrganizationUnitId": 2,
  "roleCode": "SCHOOL_ADMIN",
  "accountEnabled": true
}
```

After that, run `sprint2-part1-add-real-admin-transaction.sql` manually with the real `LoginAccountId`.

## Created People

| PersonId | Label / Purpose |
| ---: | --- |
| 910001 | School A active student, active education account, 25 system top-up transactions, enrolled courses for UM-007/UM-008 happy paths |
| 910002 | School A student with already closed account for UM-004 already-closed/conflict |
| 910003 | School A student with `CLOSING` account for UM-006 account-status filter |
| 910004-910023 | School A active students for UM-006 pagination and level/class filters |
| 910024-910026 | School A not-currently-enrolled/no-account students for UM-006 No Account and Not Enrolled filters |
| 910027 | School B active student with active education account for out-of-scope school tests |
| 910028-910030 | School B students for cross-school coverage |
| 910031 | Account holder with no `SchoolEnrollment`; use for UM-004 no-org, UM-005 no-org, UM-007 no-org, UM-008 no-org tests |

Residency coverage:

- `CITIZEN`
- `VALID_PASS_HOLDER`
- No `PR` rows are created intentionally. The PR gap remains a known ADM-28/UM-006 open risk.

## Created Education Accounts

| EducationAccountId | PersonId | Status | Purpose |
| ---: | ---: | --- | --- |
| 930001 | 910001 | `ACTIVE` | School A happy path, UM-005 details/edit, UM-007 transactions, UM-008 courses |
| 930002 | 910002 | `CLOSED` | UM-004 already-closed/conflict |
| 930003 | 910003 | `CLOSING` | UM-006 account-status filter |
| 930004 | 910027 | `ACTIVE` | School B out-of-scope tests |
| 930005 | 910031 | `ACTIVE` | No-school-enrollment account holder, HQ allowed / school admin denied tests |

## Created Transactions

| Range / Id | Account | Purpose |
| --- | ---: | --- |
| 940001-940025 | 930001 | 25 top-up credit rows with `CreatedByLoginAccountId = NULL`, displays `System`, supports UM-007 pagination |
| 940026 | 930001 | Created only by `sprint2-part1-add-real-admin-transaction.sql` after real School Admin exists; supports UM-007 real admin display-name test |

## Created Courses / Enrollments / Bill

All course rows are scoped to School A (`OrganizationId = 2`) because `course.Course` is school-scoped.

| CourseId | Code | EnrollmentId | Status | Bill | Purpose |
| ---: | --- | ---: | --- | --- | --- |
| 950001 | `QA-COURSE-PENDING-NOBILL` | 960001 | `PENDING_PAYMENT` | none | UM-008 no-bill row kept with zero money fields |
| 950002 | `QA-COURSE-PENDING-BILL` | 960002 | `PENDING_PAYMENT` | 970001 | UM-008 bill field mapping |
| 950003 | `QA-COURSE-COMPLETED` | 960003 | `COMPLETED` | none | UM-008 status mapping |
| 950004 | `QA-COURSE-CANCELLED` | 960004 | `CANCELLED` | none | UM-008 `Dropped Out` mapping |
| 950005 | `QA-COURSE-EXITED` | 960005 | `EXITED` | none | UM-008 `Dropped Out` mapping |

Bill `970001` maps:

- Fee = `GrossAmount = 120.00`
- FAS Applied = `SubsidyAmount = 20.00`
- Paid = `PaidAmount = 0.00`
- Outstanding = `OutstandingAmount = 100.00`

## Cleanup

Run `scripts/test-data/sprint2-part1-cleanup.sql`. It deletes only rows with `QA_TEST_`, `QA-EA-`, `QA-COURSE`, `QA-BILL`, or the reserved high ID ranges listed above. It does not touch the existing System Admin or baseline data.
