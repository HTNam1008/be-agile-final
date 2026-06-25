using System.Globalization;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.CourseBilling.Application.Dashboard;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.EducationAccountTopUp.IGateway.Accounts;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.Dashboard.GetStudentDashboard;

internal sealed class GetStudentDashboardHandler(
    ICurrentUser currentUser,
    IStudentDirectory students,
    IEducationAccountDirectory educationAccounts,
    IEducationAccountPaymentGateway educationAccountPayments,
    IStudentDashboardCourseRepository dashboardCourses)
    : IQueryHandler<GetStudentDashboardQuery, StudentDashboardResponse>
{
    private const string StudentRole = "STUDENT";
    private static readonly CultureInfo SingaporeCulture = CultureInfo.GetCultureInfo("en-SG");

    public async Task<Result<StudentDashboardResponse>> Handle(
        GetStudentDashboardQuery query,
        CancellationToken cancellationToken)
    {
        if (!IsAuthenticatedEServiceStudent())
        {
            return Result<StudentDashboardResponse>.Failure(DashboardErrors.AuthenticatedStudentRequired);
        }

        long personId = currentUser.PersonId!.Value;

        StudentSummary? student = await students.FindByPersonIdAsync(personId, cancellationToken);
        if (student is null)
        {
            return Result<StudentDashboardResponse>.Failure(DashboardErrors.StudentNotFound);
        }

        EducationAccountSummary? account = await educationAccounts.FindByPersonIdAsync(personId, cancellationToken);
        if (account is null)
        {
            return Result<StudentDashboardResponse>.Failure(DashboardErrors.EducationAccountNotFound);
        }
        EducationAccountPaymentBalance? paymentBalance =
            await educationAccountPayments.GetAvailableBalanceAsync(personId, cancellationToken);
        decimal currentBalance = paymentBalance?.CurrentBalance ?? account.CurrentBalance;
        decimal reservedAmount = paymentBalance?.HeldBalance ?? 0m;
        decimal availableBalance = paymentBalance?.AvailableBalance ?? currentBalance;

        IReadOnlyCollection<StudentDashboardCourseSummary> courses =
            await dashboardCourses.ListCurrentCoursesAsync(personId, query.Search, query.Status, cancellationToken);
        IReadOnlyCollection<StudentDashboardCourseSummary> publishedCourses =
            NormalizeStatus(query.Status) is null
                ? await dashboardCourses.ListPublishedCoursesAsync(personId, query.Search, cancellationToken)
                : [];

        return Result<StudentDashboardResponse>.Success(new StudentDashboardResponse(
            new StudentDashboardProfileResponse(
                student.PersonId,
                student.DisplayName,
                ToGreetingName(student.DisplayName),
                student.SchoolName),
            new StudentDashboardEducationAccountResponse(
                account.EducationAccountId,
                account.AccountNumber,
                account.CurrencyCode,
                account.AccountStatusCode,
                ToAccountStatusLabel(account.AccountStatusCode),
                currentBalance,
                ToCurrencyDisplay(account.CurrencyCode, currentBalance),
                reservedAmount,
                ToCurrencyDisplay(account.CurrencyCode, reservedAmount),
                availableBalance,
                ToCurrencyDisplay(account.CurrencyCode, availableBalance)),
            new StudentDashboardCourseFilterResponse(
                Normalize(query.Search),
                NormalizeStatus(query.Status),
                GetStatusOptions()),
            courses.Select(ToResponse).ToArray(),
            publishedCourses.Select(ToResponse).ToArray()));
    }

    private bool IsAuthenticatedEServiceStudent()
    {
        return currentUser.IsAuthenticated
            && currentUser.PersonId is not null
            && currentUser.Portal == PortalCodes.EService
            && currentUser.Roles.Contains(StudentRole);
    }

    internal static StudentDashboardCourseResponse ToResponse(StudentDashboardCourseSummary course)
    {
        return new StudentDashboardCourseResponse(
            course.CourseEnrollmentId,
            course.CoursePaymentPlanId,
            course.CourseId,
            course.CourseCode,
            course.CourseName,
            course.LecturerName,
            string.IsNullOrWhiteSpace(course.LecturerName) ? "Name of lecture" : course.LecturerName,
            course.StartDate,
            course.EndDate,
            ToDateRangeDisplay(course.StartDate, course.EndDate),
            course.EnrollmentStatusCode,
            ToEnrollmentStatusLabel(course.EnrollmentStatusCode));
    }

    internal static string ToEnrollmentStatusLabel(string statusCode)
    {
        return statusCode.Trim().ToUpperInvariant() switch
        {
            CourseEnrollmentStatusCodes.PendingPlanSelection => "Choose payment plan",
            CourseEnrollmentStatusCodes.PendingPayment => "Payment pending",
            CourseEnrollmentStatusCodes.Active => "Active",
            CourseEnrollmentStatusCodes.PaymentPastDue => "Payment past due",
            CourseEnrollmentStatusCodes.PaidInFull => "Paid in full",
            CourseEnrollmentStatusCodes.Refunded => "Refunded",
            CourseEnrollmentStatusCodes.Completed => "Completed",
            CourseEnrollmentStatusCodes.Cancelled => "Cancelled",
            CourseEnrollmentStatusCodes.Exited => "Exited",
            "AVAILABLE" => "Available",
            _ => statusCode
        };
    }

    internal static string ToAccountStatusLabel(string statusCode)
    {
        return statusCode.Trim().ToUpperInvariant() switch
        {
            "ACTIVE" => "Active",
            "PENDING" => "Pending",
            "CLOSING" => "Closing",
            "CLOSED" => "Closed",
            _ => statusCode
        };
    }

    internal static string ToCurrencyDisplay(string currencyCode, decimal amount)
    {
        return currencyCode.Trim().ToUpperInvariant() switch
        {
            "SGD" => string.Create(SingaporeCulture, $"SGD {amount:N2}"),
            _ => string.Create(CultureInfo.InvariantCulture, $"{currencyCode} {amount:N2}")
        };
    }

    internal static string ToDateRangeDisplay(DateOnly startDate, DateOnly? endDate)
    {
        return endDate is null
            ? startDate.ToString("dd MMM yyyy", SingaporeCulture)
            : string.Create(SingaporeCulture, $"{startDate:dd MMM yyyy} - {endDate.Value:dd MMM yyyy}");
    }

    internal static string ToGreetingName(string displayName)
    {
        string trimmed = displayName.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? "Student" : trimmed;
    }

    internal static string? Normalize(string? value)
    {
        string? trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    internal static string? NormalizeStatus(string? status)
    {
        return Normalize(status)?.ToUpperInvariant() switch
        {
            null => null,
            "IN_PROGRESS" or "INPROGRESS" => CourseEnrollmentStatusCodes.Active,
            var normalized => normalized
        };
    }

    internal static IReadOnlyCollection<StudentDashboardStatusOptionResponse> GetStatusOptions()
    {
        return
        [
            new StudentDashboardStatusOptionResponse(CourseEnrollmentStatusCodes.PendingPlanSelection, "Choose payment plan"),
            new StudentDashboardStatusOptionResponse(CourseEnrollmentStatusCodes.PendingPayment, "Payment pending"),
            new StudentDashboardStatusOptionResponse(CourseEnrollmentStatusCodes.Active, "Active"),
            new StudentDashboardStatusOptionResponse(CourseEnrollmentStatusCodes.PaymentPastDue, "Payment past due"),
            new StudentDashboardStatusOptionResponse(CourseEnrollmentStatusCodes.PaidInFull, "Paid in full"),
            new StudentDashboardStatusOptionResponse(CourseEnrollmentStatusCodes.Completed, "Completed"),
            new StudentDashboardStatusOptionResponse(CourseEnrollmentStatusCodes.Cancelled, "Cancelled")
        ];
    }
}
