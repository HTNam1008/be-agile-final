using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.CourseBilling.Application.Dashboard.GetStudentDashboard;

internal sealed record GetStudentDashboardSummaryQuery
    : IQuery<StudentDashboardSummaryResponse>;

