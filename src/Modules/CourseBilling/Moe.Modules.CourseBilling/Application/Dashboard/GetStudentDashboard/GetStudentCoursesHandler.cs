using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.CourseBilling.Application.Dashboard;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.Dashboard.GetStudentDashboard;

internal sealed class GetStudentCoursesHandler(
    ICurrentUser currentUser,
    IStudentDashboardCourseRepository dashboardCourses)
    : IQueryHandler<GetStudentCoursesQuery, StudentCoursesResponse>
{
    private const string StudentRole = "STUDENT";

    public async Task<Result<StudentCoursesResponse>> Handle(
        GetStudentCoursesQuery query,
        CancellationToken cancellationToken)
    {
        if (!IsAuthenticatedEServiceStudent())
        {
            return Result<StudentCoursesResponse>.Failure(DashboardErrors.AuthenticatedStudentRequired);
        }

        long personId = currentUser.PersonId!.Value;
        IReadOnlyCollection<StudentDashboardCourseSummary> courses =
            await dashboardCourses.ListCurrentCoursesAsync(personId, query.Search, query.Status, cancellationToken);
        IReadOnlyCollection<StudentDashboardCourseSummary> publishedCourses =
            GetStudentDashboardHandler.NormalizeStatus(query.Status) is null
                ? await dashboardCourses.ListPublishedCoursesAsync(personId, query.Search, cancellationToken)
                : [];

        return Result<StudentCoursesResponse>.Success(new StudentCoursesResponse(
            new StudentDashboardCourseFilterResponse(
                GetStudentDashboardHandler.Normalize(query.Search),
                GetStudentDashboardHandler.NormalizeStatus(query.Status),
                GetStudentDashboardHandler.GetStatusOptions()),
            courses.Select(GetStudentDashboardHandler.ToResponse).ToArray(),
            publishedCourses.Select(GetStudentDashboardHandler.ToResponse).ToArray()));
    }

    private bool IsAuthenticatedEServiceStudent()
    {
        return currentUser.IsAuthenticated
            && currentUser.PersonId is not null
            && currentUser.Portal == PortalCodes.EService
            && currentUser.Roles.Contains(StudentRole);
    }
}

