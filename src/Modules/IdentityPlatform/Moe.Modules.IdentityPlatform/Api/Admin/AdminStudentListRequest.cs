using Moe.Modules.IdentityPlatform.IGateway.Students;

namespace Moe.Modules.IdentityPlatform.Api.Admin;

public sealed record AdminStudentListRequest(
    string? Search,
    string? LevelCode,
    string? ClassCode,
    AdminStudentAccountStatusFilter AccountStatus = AdminStudentAccountStatusFilter.All,
    AdminStudentResidencyFilter Residency = AdminStudentResidencyFilter.All,
    AdminStudentEnrollmentStatusFilter EnrollmentStatus = AdminStudentEnrollmentStatusFilter.All,
    int Page = 1,
    int PageSize = 20);
