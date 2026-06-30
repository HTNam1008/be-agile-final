using Moe.Modules.IdentityPlatform.IGateway.Students;

namespace Moe.Modules.IdentityPlatform.Api.Admin;

public sealed record AdminStudentListRequest(
    long? OrganizationId,
    string? Search,
    string? LevelCode,
    string? ClassCode,
    AdminStudentAccountStatusFilter AccountStatus = AdminStudentAccountStatusFilter.All,
    AdminStudentEnrollmentStatusFilter EnrollmentStatus = AdminStudentEnrollmentStatusFilter.All,
    int Page = 1,
    int PageSize = 10,
    string? SortBy = null,
    string? SortDirection = null);
