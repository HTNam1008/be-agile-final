using Moe.SharedKernel.Domain;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Domain.Courses;

internal sealed class CourseEnrollment : Entity<long>
{
    private CourseEnrollment() : base(0) { }

    private CourseEnrollment(
        long personId,
        long courseId,
        long? coursePaymentPlanId,
        string enrollmentSourceCode,
        long enrolledByLoginAccountId,
        DateTime enrolledAtUtc,
        decimal beforeStartRefundPercentage,
        decimal afterStartRefundPercentage,
        string enrollmentStatusCode) : base(0)
    {
        PersonId = personId;
        CourseId = courseId;
        CoursePaymentPlanId = coursePaymentPlanId;
        EnrollmentSourceCode = enrollmentSourceCode;
        EnrolledByLoginAccountId = enrolledByLoginAccountId;
        EnrolledAtUtc = enrolledAtUtc;
        BeforeStartRefundPercentage = beforeStartRefundPercentage;
        AfterStartRefundPercentage = afterStartRefundPercentage;
        EnrollmentStatusCode = enrollmentStatusCode;
    }

    public long PersonId { get; private set; }
    public long CourseId { get; private set; }
    public long? CoursePaymentPlanId { get; private set; }
    public string EnrollmentSourceCode { get; private set; } = string.Empty;
    public long EnrolledByLoginAccountId { get; private set; }
    public DateTime EnrolledAtUtc { get; private set; }
    public string EnrollmentStatusCode { get; private set; } = string.Empty;
    public decimal BeforeStartRefundPercentage { get; private set; }
    public decimal AfterStartRefundPercentage { get; private set; }
    public DateTime? ExitAtUtc { get; private set; }
    public string? ExitReasonCode { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<CourseEnrollment> EnrollByAdmin(
        long personId,
        long courseId,
        long coursePaymentPlanId,
        long adminLoginAccountId,
        DateTime enrolledAtUtc,
        decimal beforeStartRefundPercentage,
        decimal afterStartRefundPercentage)
    {
        Result validation = ValidateEnrollment(
            personId,
            courseId,
            coursePaymentPlanId,
            adminLoginAccountId,
            beforeStartRefundPercentage,
            afterStartRefundPercentage);
        if (validation.IsFailure)
        {
            return Result<CourseEnrollment>.Failure(validation.Error);
        }

        CourseEnrollment enrollment = new(
            personId,
            courseId,
            coursePaymentPlanId,
            CourseEnrollmentSourceCodes.AdminAdd,
            adminLoginAccountId,
            enrolledAtUtc,
            beforeStartRefundPercentage,
            afterStartRefundPercentage,
            CourseEnrollmentStatusCodes.PendingPayment);

        return Result<CourseEnrollment>.Success(enrollment);
    }

    public static Result<CourseEnrollment> EnrollByAdminPendingPlanSelection(
        long personId,
        long courseId,
        long adminLoginAccountId,
        DateTime enrolledAtUtc,
        decimal beforeStartRefundPercentage,
        decimal afterStartRefundPercentage)
    {
        Result validation = ValidateEnrollmentWithoutPaymentPlan(
            personId,
            courseId,
            adminLoginAccountId,
            beforeStartRefundPercentage,
            afterStartRefundPercentage);
        if (validation.IsFailure)
        {
            return Result<CourseEnrollment>.Failure(validation.Error);
        }

        CourseEnrollment enrollment = new(
            personId,
            courseId,
            null,
            CourseEnrollmentSourceCodes.AdminAdd,
            adminLoginAccountId,
            enrolledAtUtc,
            beforeStartRefundPercentage,
            afterStartRefundPercentage,
            CourseEnrollmentStatusCodes.PendingPlanSelection);

        return Result<CourseEnrollment>.Success(enrollment);
    }

    public static Result<CourseEnrollment> JoinSelf(
        long personId,
        long courseId,
        long coursePaymentPlanId,
        long loginAccountId,
        DateTime enrolledAtUtc,
        decimal beforeStartRefundPercentage,
        decimal afterStartRefundPercentage)
    {
        Result validation = ValidateEnrollment(
            personId,
            courseId,
            coursePaymentPlanId,
            loginAccountId,
            beforeStartRefundPercentage,
            afterStartRefundPercentage);
        if (validation.IsFailure)
        {
            return Result<CourseEnrollment>.Failure(validation.Error);
        }

        CourseEnrollment enrollment = new(
            personId,
            courseId,
            coursePaymentPlanId,
            CourseEnrollmentSourceCodes.SelfJoin,
            loginAccountId,
            enrolledAtUtc,
            beforeStartRefundPercentage,
            afterStartRefundPercentage,
            CourseEnrollmentStatusCodes.PendingPayment);

        return Result<CourseEnrollment>.Success(enrollment);
    }

    public void Cancel(DateTime utcNow)
    {
        EnrollmentStatusCode = CourseEnrollmentStatusCodes.Cancelled;
        ExitAtUtc = utcNow;
        ExitReasonCode = "ADMIN_REMOVED";
    }

    public void ActivateInstallmentEnrollment()
    {
        EnrollmentStatusCode = CourseEnrollmentStatusCodes.Active;
        ExitAtUtc = null;
        ExitReasonCode = null;
    }


    public void ChangePaymentPlan(long coursePaymentPlanId, bool installment)
    {
        if (coursePaymentPlanId <= 0)
            throw new ArgumentOutOfRangeException(nameof(coursePaymentPlanId));
        CoursePaymentPlanId = coursePaymentPlanId;
        EnrollmentStatusCode = installment
            ? CourseEnrollmentStatusCodes.Active
            : CourseEnrollmentStatusCodes.PendingPayment;
        ExitAtUtc = null;
        ExitReasonCode = null;
    }

    public void GrantPaidAccess(bool paidInFull)
    {
        EnrollmentStatusCode = paidInFull
            ? CourseEnrollmentStatusCodes.PaidInFull
            : CourseEnrollmentStatusCodes.Active;
        ExitAtUtc = null;
        ExitReasonCode = null;
    }

    public void LockForPaymentFailure()
    {
        if (EnrollmentStatusCode != CourseEnrollmentStatusCodes.PaidInFull)
            EnrollmentStatusCode = CourseEnrollmentStatusCodes.PaymentPastDue;
    }

    public void MarkRefunded(DateTime utcNow)
    {
        EnrollmentStatusCode = CourseEnrollmentStatusCodes.Refunded;
        ExitAtUtc = utcNow;
        ExitReasonCode = "PAYMENT_REFUNDED";
    }

    private static Result ValidateEnrollment(
        long personId,
        long courseId,
        long coursePaymentPlanId,
        long enrolledByLoginAccountId,
        decimal beforeStartRefundPercentage,
        decimal afterStartRefundPercentage)
    {
        Result baseValidation = ValidateEnrollmentWithoutPaymentPlan(
            personId,
            courseId,
            enrolledByLoginAccountId,
            beforeStartRefundPercentage,
            afterStartRefundPercentage);
        if (baseValidation.IsFailure)
        {
            return baseValidation;
        }

        if (coursePaymentPlanId <= 0)
        {
            return Result.Failure(CourseBillingErrors.InvalidPaymentPlan);
        }

        return Result.Success();
    }

    private static Result ValidateEnrollmentWithoutPaymentPlan(
        long personId,
        long courseId,
        long enrolledByLoginAccountId,
        decimal beforeStartRefundPercentage,
        decimal afterStartRefundPercentage)
    {
        if (personId <= 0)
        {
            return Result.Failure(CourseBillingErrors.InvalidPerson);
        }

        if (courseId <= 0)
        {
            return Result.Failure(CourseBillingErrors.InvalidCourse);
        }

        if (enrolledByLoginAccountId <= 0)
        {
            return Result.Failure(CourseBillingErrors.ActorRequired);
        }

        if (beforeStartRefundPercentage is < 0m or > 100m ||
            afterStartRefundPercentage is < 0m or > 100m)
        {
            return Result.Failure(CourseErrors.InvalidRefundPercentage);
        }

        return Result.Success();
    }
}

public static class CourseEnrollmentSourceCodes
{
    public const string AdminAdd = "ADMIN_ADD";
    public const string SelfJoin = "SELF_JOIN";
}



public static class CourseBillingErrors
{
    public static readonly Error InvalidPerson = new("COURSE.INVALID_PERSON", "A valid person is required.");
    public static readonly Error InvalidCourse = new("COURSE.INVALID_COURSE", "A valid course is required.");
    public static readonly Error InvalidPaymentPlan = new("COURSE.INVALID_PAYMENT_PLAN", "A valid course payment plan is required.");
    public static readonly Error PaymentPlanNotFound = new("COURSE.PAYMENT_PLAN_NOT_FOUND", "The selected course payment plan was not found.");
    public static readonly Error PaymentPlanChangeNotAllowed = new("COURSE.PAYMENT_PLAN_CHANGE_NOT_ALLOWED", "The payment plan cannot be changed after a payment has been applied.");
    public static readonly Error FasVoucherUnavailable = new("COURSE.FAS_VOUCHER_UNAVAILABLE", "One or more selected FAS vouchers are no longer available for this course.");
    public static readonly Error InvalidStatementPeriod = new("BILL.INVALID_STATEMENT_PERIOD", "The billing statement period is invalid.");
    public static readonly Error PersonNotFound = new("COURSE.PERSON_NOT_FOUND", "The person was not found.");
    public static readonly Error CourseNotFound = new("COURSE.NOT_FOUND", "The course was not found.");
    public static readonly Error DuplicateEnrollment = new("COURSE.ENROLLMENT_DUPLICATE", "The person is already enrolled in this course.");
    public static readonly Error ActorRequired = new("COURSE.ACTOR_REQUIRED", "An authenticated login account is required.");
    public static readonly Error StudentIdentityRequired = new("COURSE.STUDENT_IDENTITY_REQUIRED", "An authenticated student identity is required.");
    public static readonly Error CourseFeesNotConfigured = new("COURSE.FEES_NOT_CONFIGURED", "The course has no active fee lines to bill.");
    public static readonly Error PersonNotInCourseOrganization = new("COURSE.PERSON_NOT_IN_ORGANIZATION", "The person is not actively enrolled in the course organization.");
    public static readonly Error CourseOrganizationForbidden = new("COURSE.ORGANIZATION_FORBIDDEN", "The administrator cannot manage courses in this organization.");
    public static readonly Error OrganizationOutsideScope = new("AUTH.ORGANIZATION_OUTSIDE_SCOPE", "The requested organization is outside the current admin's scope.");
    public static readonly Error CourseContentNotOpen = new("COURSE.CONTENT_NOT_OPEN", "Course content is available from the course start date.");
    public static readonly Error CourseContentLocked = new("COURSE.CONTENT_LOCKED", "Course content is locked for this enrollment status.");
}
