namespace Moe.Modules.IdentityPlatform.IGateway.Students;

public sealed record AdminStudentListCriteria(
    long? OrganizationId,
    string? Search,
    string? LevelCode,
    string? ClassCode,
    AdminStudentAccountStatusFilter AccountStatus,
    AdminStudentResidencyFilter Residency,
    AdminStudentEnrollmentStatusFilter EnrollmentStatus,
    int Page,
    int PageSize)
{
    public static AdminStudentListCriteria Default(
        long? organizationId = null,
        string? search = null,
        string? levelCode = null,
        string? classCode = null,
        AdminStudentAccountStatusFilter accountStatus = AdminStudentAccountStatusFilter.All,
        AdminStudentResidencyFilter residency = AdminStudentResidencyFilter.All,
        AdminStudentEnrollmentStatusFilter enrollmentStatus = AdminStudentEnrollmentStatusFilter.All,
        int page = 1,
        int pageSize = 20)
        => new(organizationId, search, levelCode, classCode, accountStatus, residency, enrollmentStatus, page, pageSize);
}

public enum AdminStudentAccountStatusFilter
{
    All,
    Active,
    PendingClosure,
    Closed,
    NoAccount
}

public enum AdminStudentResidencyFilter
{
    All,
    SingaporeCitizen,
    PermanentResident,
    Foreigner
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
    string? LevelCode,
    string? ClassCode,
    string? AccountStatusCode,
    decimal? Balance,
    string CitizenshipStatusCode,
    string EnrollmentStatusCode,
    long? OrganizationId);
