namespace Moe.Modules.IdentityPlatform.IGateway.Students;

public sealed record AdminStudentListCriteria(
    long? OrganizationId,
    string? Search,
    IReadOnlyCollection<string> LevelCodes,
    string? ClassCode,
    AdminStudentAccountStatusFilter AccountStatus,
    AdminStudentEnrollmentStatusFilter EnrollmentStatus,
    int Page,
    int PageSize,
    string? SortBy,
    string? SortDirection)
{
    public static AdminStudentListCriteria Default(
        long? organizationId = null,
        string? search = null,
        string? levelCode = null,
        IReadOnlyCollection<string>? levelCodes = null,
        string? classCode = null,
        AdminStudentAccountStatusFilter accountStatus = AdminStudentAccountStatusFilter.All,
        AdminStudentEnrollmentStatusFilter enrollmentStatus = AdminStudentEnrollmentStatusFilter.All,
        int page = 1,
        int pageSize = 10,
        string? sortBy = null,
        string? sortDirection = null)
        => new(
            organizationId,
            search,
            levelCodes ?? (string.IsNullOrWhiteSpace(levelCode) ? [] : [levelCode]),
            classCode,
            accountStatus,
            enrollmentStatus,
            page,
            pageSize,
            sortBy,
            sortDirection);
}

public enum AdminStudentAccountStatusFilter
{
    All,
    Active,
    PendingClosure,
    Closed,
    NoAccount
}

public enum AdminStudentEnrollmentStatusFilter
{
    All,
    Enrolled,
    NotEnrolled
}

public sealed record AdminStudentListPage(
    IReadOnlyList<AdminStudentListItem> Items,
    int Page,
    int PageSize,
    long TotalCount);

public sealed record AdminStudentListItem(
    long PersonId,
    string? StudentNumber,
    string? MaskedNric,
    string FullName,
    string NationalityCode,
    string? LevelCode,
    string? ClassCode,
    string? AccountStatusCode,
    decimal? Balance,
    string EnrollmentStatusCode,
    long? OrganizationId,
    string? SchoolName);
