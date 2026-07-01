namespace Moe.Modules.Notifications.Domain.Notifications;

public static class NotificationCatalog
{
    public static readonly IReadOnlyList<NotificationCatalogItem> Items =
    [
        new(NotificationTypeCode.TopUpReceived, NotificationSourceEpicCode.TopUp, NotificationPriorityCode.High, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "Student Support Received", "Amount {Amount} has been credited to account {AccountNumber}."),
        new(NotificationTypeCode.CampaignLaunch, NotificationSourceEpicCode.TopUp, NotificationPriorityCode.Medium, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "New Support Campaign: {CampaignCode}", "{CampaignName} has been launched for eligible students."),
        new(NotificationTypeCode.TopUpFailure, NotificationSourceEpicCode.TopUp, NotificationPriorityCode.High, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "Top-up Transfer Failed", "Reason: {FailureReason}. Please contact the administrator."),
        new(NotificationTypeCode.RunCompleted, NotificationSourceEpicCode.TopUp, NotificationPriorityCode.Low, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "Top-up Run {RunId} Completed", "Total succeeded: {TotalSucceeded} student(s). Total amount processed: {TotalAmount}."),
        new(NotificationTypeCode.RecurringAlert, NotificationSourceEpicCode.TopUp, NotificationPriorityCode.Medium, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "Recurring Allowance Set", "Your next support payment is scheduled for {NextRunAt} (Frequency: {FrequencyCode})."),

        new(NotificationTypeCode.FasSubmitted, NotificationSourceEpicCode.Fas, NotificationPriorityCode.Medium, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "FAS Application Received", "Application {ApplicationNumber} for {FASSchemeCode} has been submitted."),
        new(NotificationTypeCode.FasEligible, NotificationSourceEpicCode.Fas, NotificationPriorityCode.High, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "FAS Result: ELIGIBLE", "You qualify for {TierName} based on household income."),
        new(NotificationTypeCode.FasApplied, NotificationSourceEpicCode.Fas, NotificationPriorityCode.Medium, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "FAS Subsidy Applied", "Applied amount {AppliedAmount} from {FASSchemeCode} has been applied to your current bill line."),
        new(NotificationTypeCode.FasIncomeCheck, NotificationSourceEpicCode.Fas, NotificationPriorityCode.High, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "Income Verification Warning", "Your per-capita income snapshot of {PerCapitaIncomeSnapshot} is being re-evaluated for {TierName}."),
        new(NotificationTypeCode.FasExpiry, NotificationSourceEpicCode.Fas, NotificationPriorityCode.Medium, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "FAS Scheme Ending", "The scheme {FASSchemeCode} expires on {EffectiveTo}. Plan accordingly."),
        new(NotificationTypeCode.FasConfirm, NotificationSourceEpicCode.Fas, NotificationPriorityCode.High, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "Action Required: Confirm FAS", "Please confirm your eligibility status to receive benefits for {CourseName}."),

        new(NotificationTypeCode.EnrollOpen, NotificationSourceEpicCode.Courses, NotificationPriorityCode.Medium, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "Enrollment Open: {CourseCode}", "Registration for {CourseName} is open until {EnrollmentCloseAt}."),
        new(NotificationTypeCode.EnrollSuccess, NotificationSourceEpicCode.Courses, NotificationPriorityCode.Medium, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "Enrolled: {OrganizationName}", "Welcome {FullName}! You are now enrolled in {ClassCode} for Academic Year {AcademicYear}."),
        new(NotificationTypeCode.CourseDisabled, NotificationSourceEpicCode.Courses, NotificationPriorityCode.High, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "Course Suspended", "Reason: {DisabledReason}. The course {CourseCode} is currently unavailable."),
        new(NotificationTypeCode.TargetAlert, NotificationSourceEpicCode.Courses, NotificationPriorityCode.Low, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "Targeted Module for {LevelCode}", "New course available for your level: {LevelCode} in {OrganizationName}."),
        new(NotificationTypeCode.CourseExit, NotificationSourceEpicCode.Courses, NotificationPriorityCode.Low, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "Course Completed", "Result: {ExitReasonCode}. Your journey in {CourseName} has ended."),

        new(NotificationTypeCode.BillIssued, NotificationSourceEpicCode.Billing, NotificationPriorityCode.High, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "Bill Issued: {BillNumber}", "New bill for {CourseName}. Net payable: {NetPayableAmount}. Due: {DueDate}."),
        new(NotificationTypeCode.BillSubsidy, NotificationSourceEpicCode.Billing, NotificationPriorityCode.Medium, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "Subsidy Snapshot", "Subsidy amount {SubsidyAmount} has been subtracted from gross amount {GrossAmount}."),
        new(NotificationTypeCode.BillLineDetail, NotificationSourceEpicCode.Billing, NotificationPriorityCode.Low, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "Bill Line: {DescriptionSnapshot}", "Component: {ComponentCode}. Unit amount: {UnitAmount}. Quantity: {Quantity}."),
        new(NotificationTypeCode.BillOverdue, NotificationSourceEpicCode.Billing, NotificationPriorityCode.High, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "Urgent: Bill Overdue", "Bill {BillNumber} is now outstanding. Please pay {OutstandingAmount} immediately."),

        new(NotificationTypeCode.PaymentSuccess, NotificationSourceEpicCode.Payment, NotificationPriorityCode.High, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "Payment Receipt: {ReceiptNumber}", "Successful amount: {SuccessfulAmount}. Your payment was completed at {CompletedAt}."),
        new(NotificationTypeCode.PaymentFail, NotificationSourceEpicCode.Payment, NotificationPriorityCode.High, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "Payment Part Failed", "Reason: {FailureReason}. Part amount of {PartAmount} failed processing."),
        new(NotificationTypeCode.HoldActive, NotificationSourceEpicCode.Payment, NotificationPriorityCode.Medium, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "Funds On Hold", "Hold amount {HoldAmount} is active. Expires: {ExpiresAt}."),
        new(NotificationTypeCode.GatewayRef, NotificationSourceEpicCode.Payment, NotificationPriorityCode.Low, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "Gateway Transaction Logged", "Provider code: {ProviderCode}. Reference: {ProviderReference}."),

        new(NotificationTypeCode.AccOpened, NotificationSourceEpicCode.Account, NotificationPriorityCode.High, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "Account Opened: {AccountNumber}", "Reason: {OpeningReason}."),
        new(NotificationTypeCode.AccClosed, NotificationSourceEpicCode.Account, NotificationPriorityCode.High, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "Account Closed", "Reason: {ClosingReason}."),
        new(NotificationTypeCode.ExceptionApproved, NotificationSourceEpicCode.Account, NotificationPriorityCode.High, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "Extension Approved", "Reason: {ClosureExceptionReason}. Closure exception granted until {ClosureExceptionUntil}."),
        new(NotificationTypeCode.SettlementPref, NotificationSourceEpicCode.Account, NotificationPriorityCode.Medium, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "Settlement Preference Updated", "Destination: {DestinationMasked}. Status: {Status}."),
        new(NotificationTypeCode.SettlementCompleted, NotificationSourceEpicCode.Account, NotificationPriorityCode.High, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "Final Settlement: {SettlementAmount}", "Provider reference: {ProviderReference}. Account closure in progress."),

        new(NotificationTypeCode.AuditAction, NotificationSourceEpicCode.Audit, NotificationPriorityCode.Low, $"{NotificationChannelCode.Email},{NotificationChannelCode.InApp}", "Security: New Login Detected", "Actor: {ActorNameSnapshot} performed {ActionCode} from IP {IpAddress}.")
    ];

    public static bool IsKnown(string typeCode)
        => Items.Any(item => string.Equals(item.TypeCode, typeCode, StringComparison.OrdinalIgnoreCase));

    public static NotificationCatalogItem Get(string typeCode)
        => Items.First(item => string.Equals(item.TypeCode, typeCode, StringComparison.OrdinalIgnoreCase));

    public static string GetDefaultChannelCode(string typeCode)
        => Get(typeCode).DefaultChannelCodes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .First();
}
