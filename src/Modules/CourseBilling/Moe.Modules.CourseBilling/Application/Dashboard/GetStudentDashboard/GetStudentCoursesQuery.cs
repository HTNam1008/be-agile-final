using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.CourseBilling.Application.Dashboard.GetStudentDashboard;

internal sealed record GetStudentCoursesQuery(string? Search, string? Status)
    : IQuery<StudentCoursesResponse>;

