using Moe.Application.Abstractions.Messaging;
using Moe.Modules.IdentityPlatform.IGateway.Students;

namespace Moe.Modules.IdentityPlatform.Application.AdminStudentList;

internal sealed record ListAdminStudentsQuery(
    string? Search,
    string? LevelCode,
    string? ClassCode,
    AdminStudentAccountStatusFilter AccountStatus,
    AdminStudentResidencyFilter Residency,
    AdminStudentEnrollmentStatusFilter EnrollmentStatus,
    int Page,
    int PageSize) : IQuery<AdminStudentListPage>;
