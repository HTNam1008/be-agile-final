# Mail API Test Script

The consolidated result, recipient mapping, authentication requirements, and deferred payment test notes are documented in `docs/MAIL_SERVICE_TEST_GUIDE.md`.

This folder contains one executable test script:

```text
scripts/mail-test/Test-MailFlows.ps1
```

It calls real local APIs with generated mock data or IDs supplied by the tester. These calls change local database data. Run them only against a local/test database.

## Quick Test: NOTI-07

Start the API, obtain an admin access token, then run:

```powershell
$env:MOE_ADMIN_TOKEN = "<admin access token>"
$env:MOE_ADMIN_MFA_SESSION = "<moe_admin_mfa_session cookie value>"
./scripts/mail-test/Test-MailFlows.ps1 `
  -Flow AccountCreation `
  -ExpectedRecipientEmail "tphatdn1@gmail.com"
```

The script performs:

```text
POST /api/admin/v1/students
-> creates a mock Person and SchoolEnrollment

POST /api/admin/v1/education-accounts
-> creates an Education Account
-> enqueues NOTI-07
-> background worker sends the email
```

## Positive Email Tests: NOTI-01 and NOTI-11

These flows deliberately create eligible local test data so an email is expected. They do not weaken production eligibility rules.

Requirements:

```text
MOE_ADMIN_TOKEN: fresh Entra admin token
MOE_ADMIN_MFA_SESSION: matching admin MFA cookie
MOE_STUDENT_TOKEN: fresh e-Service token
OrganizationId: organization of the student represented by MOE_STUDENT_TOKEN
MailDelivery:Enabled: true
API: running on https://localhost:7000
```

NOTI-01 positive test:

```powershell
./scripts/mail-test/Test-MailFlows.ps1 `
  -Flow MonthlyBillPositive `
  -OrganizationId 3 `
  -ExpectedRecipientEmail "tphatt23467@gmail.com" `
  -WaitSeconds 15
```

The script creates a published course, full-payment plan, self-enrollment and outstanding bill. It then calls the matching billing-statement endpoint. A real NOTI-01 email is expected.

NOTI-11 positive preparation:

```powershell
./scripts/mail-test/Test-MailFlows.ps1 `
  -Flow MissedInstallmentPrepare `
  -OrganizationId 3 `
  -ExpectedRecipientEmail "tphatt23467@gmail.com" `
  -AllowLocalDatabaseMutation `
  -WaitSeconds 0
```

This flow is localhost-only. It creates an installment enrollment and updates only the generated test bill's due date to yesterday. Restart the API afterward:

```powershell
dotnet run --no-build `
  --project src/Hosts/Moe.StudentFinance.Api/Moe.StudentFinance.Api.csproj `
  --launch-profile Moe.StudentFinance.Api
```

`MissedInstallmentPaymentEmailWorker` runs immediately at startup. A real NOTI-11 email is expected.

Confirm SMTP acceptance:

```powershell
Select-String `
  -Path src/Hosts/Moe.StudentFinance.Api/bin/Debug/net10.0/Logs/*.log `
  -Pattern "Queued email delivered" | Select-Object -Last 10
```

`Queued email delivered` means SMTP accepted the message. Also check Gmail Inbox, Spam and Promotions.

For admin flows, the script first calls `POST /api/admin/v1/auth/session` with the Entra bearer token. It retains the returned `moe_admin_session` cookie for subsequent API calls, matching the backend authentication flow.

A newly created student does not have an `iam.LoginAccount` row. In Development, NOTI-07 therefore goes to `MailDelivery:DevelopmentFallbackRecipient`, currently `tphatdn1@gmail.com`. The `email` passed to `POST /students` is stored on `person.Person`; it is not the recipient used by this mail policy.

To test with a real account holder email, pass an existing `PersonId` that has no Education Account yet and has a STUDENT LoginAccount:

```powershell
./scripts/mail-test/Test-MailFlows.ps1 `
  -Flow AccountCreation `
  -PersonId 922284 `
  -ExpectedRecipientEmail "student@gmail.com"
```

## Recipient Check

All account-holder/student notifications except FAS use:

```sql
DECLARE @PersonId bigint = 922284;

SELECT TOP (1)
    LoginAccountId,
    PersonId,
    RoleCode,
    ContactEmail
FROM iam.LoginAccount
WHERE PersonId = @PersonId
  AND RoleCode = 'STUDENT'
ORDER BY LoginAccountId DESC;
```

FAS notifications use:

```sql
SELECT Email
FROM fas.FasApplication
WHERE FasApplicationId = @FasApplicationId;
```

## Available Flows

List them from the script:

```powershell
./scripts/mail-test/Test-MailFlows.ps1 -ListFlows
```

| Flow | Trigger API | Mail | Recipient |
|---|---|---|---|
| `AccountCreation` | `POST /api/admin/v1/education-accounts` | NOTI-07 | Education Account `PersonId` to newest STUDENT `ContactEmail` |
| `AdminEnrollment` | `POST /api/admin/v1/courses/{courseId}/enrollments` | NOTI-04 | Enrolled student `ContactEmail` |
| `TopUp` | `POST /api/admin/v1/top-up-campaigns/{campaignId}/runs` | NOTI-02 | Each successfully credited account holder |
| `MonthlyBill` | `GET /api/eservice/v1/billing-statements/{year}/{month}` | NOTI-01 | Authenticated student `ContactEmail` |
| `FasSubmit` | `POST /api/eservice/v1/fas/applications/{id}/submit` | NOTI-05 submit | `fas.FasApplication.Email` |
| `FasApprove` | `POST /api/admin/v1/fas/applications/{id}/approve` | NOTI-05 approved | `fas.FasApplication.Email` |
| `FasReject` | `POST /api/admin/v1/fas/applications/{id}/reject` | NOTI-05 rejected | `fas.FasApplication.Email` |
| `AccountClosure` | `POST /api/admin/v1/education-accounts/{id}/close` | NOTI-06 | Account holder `ContactEmail` |
| `Lifecycle` | `POST /api/admin/v1/education-account-lifecycle/run-now` | NOTI-06/07 and age-30 | Each affected account holder |
| `CourseWithdrawal` | `POST /api/eservice/v1/course-enrollments/{id}/cancel` | NOTI-10 | Enrollment owner `ContactEmail` |

## More Examples

Admin-added enrollment:

```powershell
./scripts/mail-test/Test-MailFlows.ps1 `
  -Flow AdminEnrollment `
  -CourseId 123 `
  -StudentNumber "STUDENT-001" `
  -ExpectedRecipientEmail "student@gmail.com"
```

Top-up campaign with configured recipients:

```powershell
./scripts/mail-test/Test-MailFlows.ps1 `
  -Flow TopUp `
  -CampaignId 8
```

Create a disposable immediate campaign and execute it:

```powershell
./scripts/mail-test/Test-MailFlows.ps1 `
  -Flow TopUp `
  -CreateMockTopUpCampaign `
  -TopUpEducationAccountIds 932220,932221 `
  -ExpectedRecipientEmail "tphatdn1@gmail.com"
```

Student flows require an e-Service token:

```powershell
$env:MOE_STUDENT_TOKEN = "<student access token>"

./scripts/mail-test/Test-MailFlows.ps1 `
  -Flow MonthlyBill `
  -BillingYear 2026 `
  -BillingMonth 7 `
  -ExpectedRecipientEmail "student@gmail.com"
```

Create a disposable self-enrollment and withdraw it without testing payment:

The student represented by `MOE_STUDENT_TOKEN` must have an active SchoolEnrollment in the same organization as the course.

```powershell
./scripts/mail-test/Test-MailFlows.ps1 `
  -Flow CourseWithdrawal `
  -CreateMockEnrollment `
  -CourseId <course-in-student-organization> `
  -CoursePaymentPlanId <active-plan-id> `
  -ExpectedRecipientEmail "<student-contact-email>"
```

Create the course, fee, payment plan and enrollment prerequisites automatically:

```powershell
./scripts/mail-test/Test-MailFlows.ps1 `
  -Flow CourseWithdrawal `
  -CreateMockCourse `
  -OrganizationId 3 `
  -ExpectedRecipientEmail "tphatt23467@gmail.com"
```

This flow does not create a checkout, make a payment or call Stripe.

Multiple flows can be requested together when all required IDs are supplied:

```powershell
./scripts/mail-test/Test-MailFlows.ps1 `
  -Flow TopUp,Lifecycle `
  -CampaignId 8
```

Create and immediately close a disposable Education Account to test NOTI-07 and NOTI-06:

```powershell
./scripts/mail-test/Test-MailFlows.ps1 `
  -Flow AccountCreation,AccountClosure `
  -ExpectedRecipientEmail "tphatdn1@gmail.com"
```

## Conditions That Still Apply

- `MailDelivery:Enabled` must be `true`, and the API must be restarted after configuration changes.
- NOTI-01 needs a newly generated/changed statement with outstanding amount greater than zero. Re-reading an unchanged statement does not enqueue another email.
- NOTI-02 needs at least one successful, non-replayed credit. A `PREVIEWED` run does not send mail.
- NOTI-05 submission needs a complete valid draft.
- NOTI-06 closure and NOTI-10 withdrawal modify real test data.
- Lifecycle only sends when a person/account matches its date and status rules.
- NOTI-03 and NOTI-09 require a correctly signed Stripe webhook. Arbitrary mock JSON cannot pass Stripe signature validation; use Stripe CLI/provider test events.
- NOTI-11 has no manual API. Its background worker checks installments due yesterday with outstanding amount greater than zero.
