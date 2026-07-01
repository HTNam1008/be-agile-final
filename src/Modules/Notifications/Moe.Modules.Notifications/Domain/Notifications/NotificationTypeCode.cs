namespace Moe.Modules.Notifications.Domain.Notifications;

public static class NotificationTypeCode
{
    public const string TopUpReceived = "TOP_UP_RECEIVED";
    public const string CampaignLaunch = "CAMPAIGN_LAUNCH";
    public const string TopUpFailure = "TOP_UP_FAILURE";
    public const string RunCompleted = "RUN_COMPLETED";
    public const string RecurringAlert = "RECURRING_ALERT";

    public const string FasSubmitted = "FAS_SUBMITTED";
    public const string FasEligible = "FAS_ELIGIBLE";
    public const string FasApplied = "FAS_APPLIED";
    public const string FasIncomeCheck = "FAS_INCOME_CHECK";
    public const string FasExpiry = "FAS_EXPIRY";
    public const string FasConfirm = "FAS_CONFIRM";

    public const string EnrollOpen = "ENROLL_OPEN";
    public const string EnrollSuccess = "ENROLL_SUCCESS";
    public const string CourseDisabled = "COURSE_DISABLED";
    public const string TargetAlert = "TARGET_ALERT";
    public const string CourseExit = "COURSE_EXIT";

    public const string BillIssued = "BILL_ISSUED";
    public const string BillSubsidy = "BILL_SUBSIDY";
    public const string BillLineDetail = "BILL_LINE_DETAIL";
    public const string BillOverdue = "BILL_OVERDUE";

    public const string PaymentSuccess = "PAYMENT_SUCCESS";
    public const string PaymentFail = "PAYMENT_FAIL";
    public const string HoldActive = "HOLD_ACTIVE";
    public const string GatewayRef = "GATEWAY_REF";

    public const string AccOpened = "ACC_OPENED";
    public const string AccClosed = "ACC_CLOSED";
    public const string ExceptionApproved = "EXCEPTION_APPROVED";
    public const string SettlementPref = "SETTLEMENT_PREF";
    public const string SettlementCompleted = "SETTLEMENT_COMPLETED";

    public const string AuditAction = "AUDIT_ACTION";
}
