using Moe.Application.Abstractions.Messaging;
using Moe.Modules.IdentityPlatform.IGateway.Students;

namespace Moe.Modules.IdentityPlatform.Application.AdminStudentList;

internal sealed record ListAdminStudentsQuery(
    long? OrganizationId,
    string? Search,
    IReadOnlyCollection<string> LevelCodes,
    string? ClassCode,
    AdminStudentAccountStatusFilter AccountStatus,
    AdminStudentEnrollmentStatusFilter EnrollmentStatus,
    int Page,
    int PageSize) : IQuery<AdminStudentListPage>;
