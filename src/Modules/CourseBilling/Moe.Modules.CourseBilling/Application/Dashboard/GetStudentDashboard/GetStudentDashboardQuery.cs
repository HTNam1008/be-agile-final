using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.CourseBilling.Application.Dashboard.GetStudentDashboard;

public sealed record GetStudentDashboardQuery(string? Search, string? Status)
    : IQuery<StudentDashboardResponse>;
