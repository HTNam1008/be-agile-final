# MOE Student Finance Mail Service Guide

## Scope

This implementation sends email only. In-app notifications and NOTI-08 notification-bell behavior are not included.

Business endpoints and scheduled jobs do not wait for SMTP. They create an in-memory email job, and `QueuedEmailDeliveryWorker` resolves the recipient and sends the email in the background.

```text
Business flow succeeds
-> EmailNotificationJob is added to IEmailNotificationQueue
-> HTTP request or scheduled business task continues
-> QueuedEmailDeliveryWorker reads the job
-> Recipient is resolved
-> IEmailDeliveryGateway sends through SMTP
```

The queue is in memory and has a capacity of 1,000 jobs. Jobs that are still waiting are lost if the API process restarts.

## Configuration

The global switch is:

```json
"MailDelivery": {
  "Enabled": true
}
```

Display brand, sender display name, and portal links are configured from:

```json
"MailDelivery": {
  "AppName": "Ministry of Education - Singapore",
  "FromDisplayName": "Ministry of Education - Singapore",
  "PortalBaseUrl": "http://localhost:5173"
}
```

Email templates must use `MailDelivery:AppName` for body/header/footer brand text. Links such as the payment dashboard, FAS portal, account portal, and course detail pages must be built from `MailDelivery:PortalBaseUrl`.

When `Enabled` is `false`:

- notification services do not enqueue jobs;
- recipient resolution is not called;
- SMTP is not called;
- business endpoints and workers continue normally.

The primary SMTP sender uses `MailDelivery:UserName` and `MailDelivery:FromEmail`. If Gmail reports a sending quota/limit error and fallback SMTP credentials are configured, the gateway retries with `FallbackUserName` and `FallbackFromEmail`.

Do not include SMTP passwords in this guide or logs.

## Account Holder Recipient

All account-holder business emails, including NOTI-01, NOTI-02, NOTI-03, NOTI-04, NOTI-05, NOTI-06, NOTI-07, NOTI-09, NOTI-10, NOTI-11, payment/defer receipts, and the age-30 reminder resolve the recipient from:

```text
table: iam.LoginAccount
field: ContactEmail
conditions:
  PersonId == notification PersonId
  RoleCode == STUDENT
order:
  newest LoginAccountId first
```

Equivalent SQL:

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

The selected value is trimmed and validated with `MailAddress.TryCreate`.

If no valid contact email exists:

- Development uses `MailDelivery:DevelopmentFallbackRecipient`, when valid.
- UAT/Production skips the email and writes a warning log.
- The business transaction and API response remain successful.

The email submitted to:

```http
POST /api/admin/v1/students
```

is stored in `person.Person.PreferredEmail` and `person.Person.OfficialEmail`. It is not used by the current mail recipient policy. To receive account-creation and account-holder emails, that person must have a STUDENT row in `iam.LoginAccount` with a valid `ContactEmail`.

## FAS Recipient

NOTI-05 follows the same account-holder recipient policy. It uses the FAS application's `AccountHolderPersonId`, then resolves the recipient from `iam.LoginAccount.ContactEmail`.

The email stored on `fas.FasApplication.Email` is treated as application form data only. It is not used as the delivery recipient.

## Trigger Matrix

### NOTI-01 Monthly Bill

Subject:

```text
Your [Month] Bill Is Ready
```

Automatic trigger:

- `MonthlyBillNotificationWorker` checks hourly.
- It processes only on the first day of each month.
- It selects account holders with outstanding bills due in that month.

Manual test endpoint:

```http
GET /api/eservice/v1/billing-statements/{year}/{month}
```

Send conditions:

- bill outstanding amount is greater than zero;
- bill status is not `PAID` or `CANCELLED`;
- enrollment is not `PENDING_PLAN_SELECTION`;
- statement is newly created or receives a new bill item.

Recipient path:

```text
BillingStatement.PersonId
-> iam.LoginAccount.PersonId
-> newest STUDENT ContactEmail
```

Calling the GET endpoint repeatedly for an unchanged statement does not enqueue duplicate email.

### NOTI-02 Government Top-Up

Subject:

```text
Funds Credited to Your Education Account
```

Manual trigger:

```http
POST /api/admin/v1/top-up-campaigns/{campaignId}/runs
```

Run status:

```http
GET /api/admin/v1/top-up/runs/{runId}
```

Scheduled top-up campaigns use the same credit flow.

Send condition:

- the balance credit completes successfully;
- the transaction is not an idempotent replay.

Recipient path:

```text
topup.TopUpTransaction.EducationAccountId
-> account.EducationAccount.PersonId
-> iam.LoginAccount.PersonId
-> newest STUDENT ContactEmail
```

Creating recipients, previewing a campaign, or seeing `PREVIEWED` status does not send email.

### NOTI-03 Successful Self Enrollment

Subject:

```text
You're Enrolled in [Course Name]
```

Entry flow:

```http
POST /api/eservice/v1/course-enrollments
```

Email trigger:

```http
POST /api/webhooks/stripe
```

Send conditions:

- enrollment source is self-join;
- payment changes enrollment to paid in full;
- the previous state was not already paid in full.

Recipient:

```text
CourseEnrollment.PersonId
-> newest STUDENT LoginAccount.ContactEmail
```

Creating the enrollment alone does not send the successful-enrollment email.

### NOTI-04 Admin-Added Enrollment

Subject:

```text
You've Been Enrolled in [Course Name]
```

Trigger:

```http
POST /api/admin/v1/courses/{courseId}/enrollments
```

Send condition:

- the enrollment source is admin-add;
- the enrollment record is created successfully.

Recipient:

```text
CourseEnrollment.PersonId
-> newest STUDENT LoginAccount.ContactEmail
```

When bill values already exist, the email includes fee payable and payment due date. Otherwise it displays:

```text
To be confirmed after payment plan selection
```

### NOTI-05 FAS Application

Subjects:

```text
We've Received Your FAS Application
Your FAS Application Has Been Approved
Update on Your FAS Application
```

Submission trigger:

```http
POST /api/eservice/v1/fas/applications/{id}/submit
```

Admin decision triggers:

```http
POST /api/admin/v1/fas/application-schemes/{id}/approve
POST /api/admin/v1/fas/application-schemes/{id}/reject
POST /api/admin/v1/fas/applications/{applicationId}/approve
POST /api/admin/v1/fas/applications/{applicationId}/reject
```

Recipient:

```text
fas.FasApplication.Email
```

### NOTI-06 Account Closure

Subjects:

```text
Your Education Account Has Been Closed
Action Required: Outstanding Balance Before Account Closure
```

Manual closure trigger:

```http
POST /api/admin/v1/education-accounts/{educationAccountId}/close
```

Lifecycle trigger:

```http
POST /api/admin/v1/education-account-lifecycle/run-now
```

The scheduled lifecycle worker uses the same closure email service.

Recipient:

```text
EducationAccount.PersonId
-> newest STUDENT LoginAccount.ContactEmail
```

### NOTI-07 Education Account Created

Subject:

```text
MOE - Your Education Account has been created!
```

Manual trigger:

```http
POST /api/admin/v1/education-accounts
```

Automatic lifecycle trigger:

```http
POST /api/admin/v1/education-account-lifecycle/run-now
```

Send condition:

- a new Education Account is created and persisted successfully.

Recipient:

```text
EducationAccount.PersonId
-> newest STUDENT LoginAccount.ContactEmail
```

Important: creating a Person through `POST /api/admin/v1/students` does not create the STUDENT LoginAccount recipient automatically. Confirm the LoginAccount row before testing NOTI-07.

### Age-30 Account Lock Reminder

Subject:

```text
Reminder: Your MOE SEEDS account will be locked soon
```

Trigger:

```http
POST /api/admin/v1/education-account-lifecycle/run-now
```

The scheduled lifecycle worker uses the same flow.

Reminder dates:

- six months before the 30th birthday;
- three months before the 30th birthday;
- one week before the 30th birthday.

Recipient:

```text
EducationAccount.PersonId
-> newest STUDENT LoginAccount.ContactEmail
```

### NOTI-08 Notification Bell

NOTI-08 is an in-app notification feature and is not implemented by the mail service.

### NOTI-09 Payment Failed

Subject:

```text
Action Required: Your Payment Failed
```

Stripe failure trigger:

```http
POST /api/webhooks/stripe
```

Statement-payment entry point:

```http
POST /api/eservice/v1/billing-statements/{statementId}/payments
```

The email is enqueued when the payment flow detects a failed, expired, declined, or otherwise unsuccessful payment.

Recipient:

```text
Payment.PayerPersonId or CourseEnrollment.PersonId
-> newest STUDENT LoginAccount.ContactEmail
```

### NOTI-10 Course Withdrawal

Subject:

```text
Your Withdrawal from [Course Name] Is Confirmed
```

Trigger:

```http
POST /api/eservice/v1/course-enrollments/{enrollmentId}/cancel
```

Send condition:

- cancellation completes successfully;
- refund information is included when a refund applies.

Recipient:

```text
CourseEnrollment.PersonId
-> newest STUDENT LoginAccount.ContactEmail
```

### NOTI-11 Missed Installment

Subject:

```text
Missed Installment Payment
```

Automatic trigger:

- `MissedInstallmentPaymentEmailWorker` runs every 24 hours;
- it scans installment bills whose due date was yesterday;
- it only enqueues email when outstanding amount is greater than zero.

Recipient:

```text
Bill.CourseEnrollmentId
-> CourseEnrollment.PersonId
-> newest STUDENT LoginAccount.ContactEmail
```

## Troubleshooting

Check these conditions when no email arrives:

1. `MailDelivery:Enabled` is `true`.
2. The API process was restarted after configuration changes.
3. The business trigger reached the successful state required by the notification.
4. A STUDENT LoginAccount exists for the notification PersonId.
5. The newest STUDENT LoginAccount has a valid `ContactEmail`.
6. The background queue was not full.
7. The API process did not restart while the email was waiting in memory.
8. SMTP credentials and Gmail quota are valid.

Useful log messages include:

```text
email enqueue failed
Queued email skipped because no valid recipient was found
Queued email delivery failed
Email delivery succeeded with fallback SMTP sender
```
