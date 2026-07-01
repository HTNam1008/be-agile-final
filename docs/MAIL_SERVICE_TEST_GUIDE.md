# Mail Service Test Guide

## Test Runner

Use:

```text
scripts/mail-test/Test-MailFlows.ps1
```

Detailed commands are in `scripts/mail-test/README.md`.

## Interpreting Test Results

There are three different outcomes:

1. `Automated/unit test passed`: uses fake/in-memory queue or gateway and never sends Gmail.
2. `Negative business case passed`: conditions intentionally do not enqueue mail, such as zero outstanding balance or no overdue installment.
3. `Live SMTP test passed`: API enqueues a real job and the log contains `Queued email delivered` after `SendMailAsync` completes.

An HTTP `200/201` alone confirms only the business endpoint. It does not prove SMTP delivery. For a live mail test, verify the delivery log and then check Gmail Inbox, Spam and Promotions.

Latest live confirmation:

```text
NotificationType: NOTI-07
EntityType: EducationAccount
EntityId: 942267
PersonId: 922287
RecipientSource: DEVELOPMENT_FALLBACK
SMTP result: Queued email delivered
Expected mailbox: tphatdn1@gmail.com
```

The NOTI-10 live test targeted `tphatt23467@gmail.com`, not `tphatdn1@gmail.com`.

Do not save access tokens or MFA cookies in the repository. Supply them through process environment variables:

```powershell
$env:MOE_ADMIN_TOKEN = "<admin Entra access token>"
$env:MOE_ADMIN_MFA_SESSION = "<moe_admin_mfa_session cookie>"
$env:MOE_STUDENT_TOKEN = "<student e-Service access token>"
```

The script exchanges the admin bearer token through:

```http
POST /api/admin/v1/auth/session
```

It retains the returned `moe_admin_session` cookie for subsequent admin requests.

## Recipient Policy

All account-holder/student email except FAS resolves the recipient using:

```sql
SELECT TOP (1)
    LoginAccountId,
    PersonId,
    ContactEmail
FROM iam.LoginAccount
WHERE PersonId = @PersonId
  AND RoleCode = 'STUDENT'
ORDER BY LoginAccountId DESC;
```

The selected field is:

```text
iam.LoginAccount.ContactEmail
```

Development behavior when no valid ContactEmail exists:

```text
MailDelivery:DevelopmentFallbackRecipient
```

Current local fallback:

```text
tphatdn1@gmail.com
```

UAT and Production do not use this fallback. Missing recipient causes the mail to be skipped and logged without failing the business flow.

FAS is the only exception:

```text
fas.FASApplication.Email
```

## NOTI-02 Recipient

Top-up tables do not store an email. Resolve it through the account owner:

```text
topup.TopUpTransaction.EducationAccountId
-> account.EducationAccount.PersonId
-> iam.LoginAccount.PersonId where RoleCode = STUDENT
-> newest iam.LoginAccount.ContactEmail
```

Equivalent diagnostic query:

```sql
DECLARE @TopUpRunId bigint = 14;

SELECT
    tt.TopUpTransactionId,
    tt.EducationAccountId,
    ea.PersonId,
    tt.Amount,
    tt.TransactionStatusCode,
    recipient.LoginAccountId,
    recipient.ContactEmail
FROM topup.TopUpTransaction tt
JOIN account.EducationAccount ea
    ON ea.EducationAccountId = tt.EducationAccountId
OUTER APPLY
(
    SELECT TOP (1)
        la.LoginAccountId,
        la.ContactEmail
    FROM iam.LoginAccount la
    WHERE la.PersonId = ea.PersonId
      AND la.RoleCode = 'STUDENT'
    ORDER BY la.LoginAccountId DESC
) recipient
WHERE tt.TopUpRunId = @TopUpRunId;
```

Run `14` used two Education Accounts whose owners had no STUDENT LoginAccount. Therefore both emails correctly used the Development fallback `tphatdn1@gmail.com`. For a real-recipient test, select Education Accounts whose owner query returns a valid `ContactEmail`.

## Verified Flows

### NOTI-01 Monthly Bill: No Outstanding Negative Case

```text
Authenticated PersonId: 2019
BillingStatementId: 2
Period: June 2026
Status: PAID
Outstanding amount: SGD 0.00
Result: endpoint returned 200 and no email was sent, as required
```

### NOTI-02 Government Top-up

```text
Mock TopUpCampaignId: 9
TopUpRunId: 14
Status: COMPLETED
Processed: 2
Succeeded: 2
Total credited: SGD 0.02
Recipient: tphatdn1@gmail.com through Development fallback
```

### NOTI-04 Admin-added Enrollment

```text
CourseEnrollmentId: 960007
CourseId: 950006
PersonId: 2010
Status: PENDING_PLAN_SELECTION
Recipient: tphatt23467@gmail.com from iam.LoginAccount.ContactEmail
```

### NOTI-06 Account Closure

```text
EducationAccountId: 942266
PersonId: 922286
Status: CLOSED
Recipient: tphatdn1@gmail.com through Development fallback
```

### NOTI-07 Account Creation

```text
EducationAccountId: 942266
PersonId: 922286
Creation status: ACTIVE
Recipient: tphatdn1@gmail.com through Development fallback
```

### NOTI-10 Course Withdrawal

```text
Authenticated PersonId: 2019
Recipient: tphatt23467@gmail.com from iam.LoginAccount.ContactEmail
Mock CourseId: 950007
Course organization: 3
CoursePaymentPlanId: 2
CourseEnrollmentId: 960008
Generated BillId: 970002
Enrollment status before withdrawal: PENDING_PAYMENT
Enrollment status after withdrawal: CANCELLED
Refund amount: SGD 0.00
Result: endpoint returned 200 and NOTI-10 was queued
```

The test created course, fee and payment-plan prerequisites but did not create a checkout, make a payment, or call Stripe.

### NOTI-11 Missed Installment: No Candidate

The worker query was checked for `2026-06-29`, one day before the API UTC date. There was no installment-plan bill satisfying all conditions:

```text
CurrentDueDate = yesterday
OutstandingAmount > 0
BillStatusCode is not PAID or CANCELLED
PlanTypeCode = INSTALLMENT
```

Result: no email was expected or sent.

## Running Positive Tests Later

Use `scripts/mail-test/Test-MailFlows.ps1` with fresh sessions. Do not paste tokens into the script or commit them.

For the previously supplied student account:

```text
PersonId: 2019
OrganizationId: 3
Recipient: iam.LoginAccount.ContactEmail = tphatt23467@gmail.com
```

NOTI-01 positive:

```powershell
$env:MOE_ADMIN_TOKEN = "<fresh admin token>"
$env:MOE_ADMIN_MFA_SESSION = "<matching MFA cookie>"
$env:MOE_STUDENT_TOKEN = "<fresh student token>"

./scripts/mail-test/Test-MailFlows.ps1 `
  -Flow MonthlyBillPositive `
  -OrganizationId 3 `
  -ExpectedRecipientEmail "tphatt23467@gmail.com"
```

NOTI-11 positive:

```powershell
./scripts/mail-test/Test-MailFlows.ps1 `
  -Flow MissedInstallmentPrepare `
  -OrganizationId 3 `
  -ExpectedRecipientEmail "tphatt23467@gmail.com" `
  -AllowLocalDatabaseMutation `
  -WaitSeconds 0
```

Then restart the API. The worker runs immediately and sends the prepared missed-installment mail.

These positive flows create disposable course/enrollment/bill data in the local database. `MissedInstallmentPrepare` refuses non-localhost URLs and requires an explicit mutation flag.

## Deep Test Scenarios

### NOTI-01 Monthly Bill

`No outstanding -> no email` means the worker and endpoint are operating, but the business eligibility query deliberately returns no recipient. It is not evidence that mail delivery is broken.

| ID | Scenario | Expected result | Coverage |
|---|---|---|---|
| N01-01 | First day of month, bill due in that month, outstanding greater than zero | Build/get statement and enqueue one NOTI-01 | Automated |
| N01-02 | Outstanding equals zero and statement is PAID | Do not enqueue | Automated and API verified with statement `2` |
| N01-03 | Worker runs on any day other than the first | Do not query/send monthly notifications | Automated |
| N01-04 | Outstanding bill is due in another month | Do not enqueue for the current billing month | Automated |
| N01-05 | Enrollment is `PENDING_PLAN_SELECTION` | Do not enqueue because payable schedule is not confirmed | Automated |
| N01-06 | One person has multiple eligible outstanding bills | Process that PersonId once and consolidate the statement | Automated |
| N01-07 | Worker is invoked twice in the same month/process | Process once through `_lastProcessedMonthStart` | Automated |
| N01-08 | Statement GET is repeated without a new bill item | Do not enqueue a duplicate statement email | Repository test coverage |
| N01-09 | Recipient has no valid STUDENT ContactEmail | Development fallback; UAT/Production skip with warning | MailDelivery resolver tests |
| N01-10 | Queue full or SMTP fails | Keep statement/API success unchanged and log warning | MailDelivery queue tests |
| N01-11 | API restarts on the first day after the worker already ran | Worker may evaluate again, but unchanged statement data must prevent duplicate enqueue | Regression scenario |
| N01-12 | UTC is day 1 while local time is still previous day, or vice versa | Use UTC date consistently | Clock-bound automated test recommended |

Relevant automated tests:

```text
MonthlyBillNotificationWorkerTests
BillingStatementRepositoryEmailTests
QueuedEmailDeliveryTests
```

### NOTI-11 Missed Installment

`No overdue candidate -> no email` means no row matched every condition. A bill must be overdue, unpaid, linked to an enrollment with a payment plan, and that plan must be `INSTALLMENT`.

| ID | Scenario | Expected result | Coverage |
|---|---|---|---|
| N11-01 | Installment bill due yesterday with outstanding amount | Enqueue NOTI-11 containing amount, course and due date | Automated |
| N11-02 | Same bill is evaluated twice by the same worker process | Enqueue once | Automated; in-memory BillId dedupe added |
| N11-03 | Plan type is `FULL_PAYMENT` | Do not enqueue | Automated |
| N11-04 | Bill is due today or in the future | Do not enqueue | Automated |
| N11-05 | Bill is PAID or outstanding equals zero | Do not enqueue | Automated |
| N11-06 | Bill is CANCELLED | Do not enqueue | Query condition; dedicated regression test recommended |
| N11-07 | Enrollment has no payment plan | Do not enqueue | Query condition |
| N11-08 | MailDelivery is disabled | Do not query plan and do not enqueue | Automated |
| N11-09 | Queue rejects the job | Log warning and allow a later retry | Retry regression test recommended |
| N11-10 | Recipient email is invalid/missing | Development fallback; UAT/Production skip | Resolver tests |
| N11-11 | SMTP fails | Business/billing data remains unchanged; worker continues | Queue worker tests |
| N11-12 | API is down on the only day the bill equals `yesterday` | Current implementation can miss the notification | Known gap |
| N11-13 | API restarts after a successful enqueue on the same eligibility day | In-memory dedupe is lost and duplicate email is possible | Known gap |

Relevant automated test:

```text
MissedInstallmentPaymentEmailWorkerTests
```

The focused NOTI-01/NOTI-11 suite currently has 10 passing tests.

## Known Gaps

### NOTI-11 Durable Delivery State

The current in-memory `BillId` dedupe protects repeated execution only while the API process remains alive. It cannot guarantee exactly-once delivery across restarts or multiple API instances.

A production-grade solution should persist a notification dispatch key such as:

```text
NOTI-11:{BillId}:{CurrentDueDate}
```

and enforce uniqueness before enqueueing. This requires durable storage/schema or an external durable queue and was not added in the current mail-only, no-schema implementation.

The worker also checks `CurrentDueDate == today - 1 day`. A catch-up design should scan all unpaid overdue installments and rely on the durable dispatch key to avoid duplicates. Until that exists, downtime can cause a missed email.

## Credentials and Data Requirements

| Notification | Required authentication | Required data |
|---|---|---|
| NOTI-01 Monthly bill | Student e-Service token | Statement month containing a new/changed outstanding bill |
| NOTI-02 Top-up | Admin token/session | New executable campaign and active Education Account recipients |
| NOTI-03 Successful self-enrollment | Student token plus valid Stripe webhook | Self-enrollment, bill, payment plan, checkout and successful Stripe event |
| NOTI-04 Admin enrollment | Admin token/session | Published course and active student in the same school |
| NOTI-05 FAS | Student token for submit; admin token for approve/reject | Completed FAS draft/application with `fas.FASApplication.Email` |
| NOTI-06 Closure | Admin token/session | Active Education Account in the admin organization scope |
| NOTI-07 Account creation | Admin token/session | Eligible Person without an Education Account |
| NOTI-09 Payment failed | Student token plus valid Stripe webhook | Pending checkout/payment and failed or expired Stripe event |
| NOTI-10 Withdrawal | Student token | Cancellable enrollment owned by that student |
| NOTI-11 Missed installment | No HTTP token; background worker | Installment due yesterday with outstanding amount greater than zero |
| Age-30 reminder | Admin token for manual lifecycle, or scheduled worker | Active account holder exactly 6 months, 3 months or 1 week before age 30 |

The student token must belong to the same `PersonId` that owns the enrollment, statement, payment, FAS application or withdrawal record being tested.

## Payment Testing Deferred

NOTI-03 and NOTI-09 payment flows will not be executed as part of the current test session. They are intentionally deferred for manual testing later.

They require:

1. A student e-Service access token for the target student.
2. An eligible published course and active payment plan.
3. A self-enrollment and generated bill or billing statement.
4. A checkout created by the payment API.
5. A correctly signed Stripe webhook containing the checkout's `checkoutId` metadata.

Relevant APIs:

```http
POST /api/eservice/v1/course-enrollments
POST /api/eservice/v1/payments/checkout-sessions
POST /api/eservice/v1/billing-statements/{statementId}/payments
POST /api/webhooks/stripe
```

Expected notification behavior:

```text
successful payment -> NOTI-03
failed/expired payment -> NOTI-09
```

Arbitrary webhook JSON is not sufficient because the backend verifies the Stripe signature.

The supplied student session belongs to:

```text
LoginAccountId: 1021
PersonId: 2019
OrganizationId: 3
ContactEmail: tphatt23467@gmail.com
```

`moe_portal_mfa_session` is not required by the current billing-statement and course-enrollment endpoints. The `moe_eservice_session` JWT is sufficient for their e-Service authorization policy.

## Not Yet Executed

- NOTI-01 positive-send case still requires an outstanding statement that has not already been notified. Its no-outstanding negative case has passed.
- NOTI-03 and NOTI-09 are deferred as described above.
- NOTI-05 has no FAS application data in the current local database.
- NOTI-10 positive flow has passed using a newly created organization `3` course.
- NOTI-11 has no qualifying overdue installment in the current database; its no-candidate behavior is correct.
- The lifecycle endpoint was not run globally because the local database currently contains many active accounts and the operation may modify all eligible records.
