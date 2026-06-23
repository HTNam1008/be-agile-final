namespace Moe.Modules.CourseBilling.Application.Dashboard.GetStudentDashboard;

public sealed record StudentDashboardResponse(
    StudentDashboardProfileResponse Student,
    StudentDashboardEducationAccountResponse EducationAccount,
    StudentDashboardCourseFilterResponse Filters,
    IReadOnlyCollection<StudentDashboardCourseResponse> CurrentCourses,
    IReadOnlyCollection<StudentDashboardCourseResponse> PublishedCourses);

public sealed record StudentDashboardSummaryResponse(
    StudentDashboardProfileResponse Student,
    StudentDashboardEducationAccountResponse EducationAccount,
    int CurrentCourseCount);

public sealed record StudentCoursesResponse(
    StudentDashboardCourseFilterResponse Filters,
    IReadOnlyCollection<StudentDashboardCourseResponse> CurrentCourses,
    IReadOnlyCollection<StudentDashboardCourseResponse> PublishedCourses);

public sealed record StudentDashboardProfileResponse(
    long PersonId,
    string DisplayName,
    string GreetingName,
    string? SchoolName);

public sealed record StudentDashboardEducationAccountResponse(
    long EducationAccountId,
    string AccountNumber,
    string CurrencyCode,
    string AccountStatusCode,
    string AccountStatusLabel,
    decimal CurrentBalance,
    string CurrentBalanceDisplay,
    decimal ReservedAmount,
    string ReservedAmountDisplay,
    decimal AvailableBalance,
    string AvailableBalanceDisplay);

public sealed record StudentDashboardCourseFilterResponse(
    string? Search,
    string? Status,
    IReadOnlyCollection<StudentDashboardStatusOptionResponse> StatusOptions);

public sealed record StudentDashboardStatusOptionResponse(
    string Value,
    string Label);

public sealed record StudentDashboardCourseResponse(
    long? CourseEnrollmentId,
    long? CoursePaymentPlanId,
    long CourseId,
    string CourseCode,
    string CourseName,
    string? LecturerName,
    string LecturerDisplay,
    DateOnly StartDate,
    DateOnly? EndDate,
    string DateRangeDisplay,
    string EnrollmentStatusCode,
    string EnrollmentStatusLabel);
