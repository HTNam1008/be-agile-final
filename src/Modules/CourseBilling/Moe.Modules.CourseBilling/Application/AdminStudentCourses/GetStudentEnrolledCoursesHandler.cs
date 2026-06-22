using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.AdminStudentCourses;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.AdminStudentCourses;

internal sealed class GetStudentEnrolledCoursesHandler(
    IPersonDirectory people,
    IAdminAccessControl adminAccess,
    IAdminStudentEnrolledCourseReader reader)
    : IQueryHandler<GetStudentEnrolledCoursesQuery, PageResponse<AdminStudentEnrolledCourseItem>>
{
    public async Task<Result<PageResponse<AdminStudentEnrolledCourseItem>>> Handle(
        GetStudentEnrolledCoursesQuery query,
        CancellationToken cancellationToken)
    {
        PersonSummary? person = await people.FindAsync(query.PersonId, cancellationToken);
        if (person is null)
        {
            return Result<PageResponse<AdminStudentEnrolledCourseItem>>.Failure(CourseBillingErrors.PersonNotFound);
        }

        if (person.OrganizationId is long organizationId)
        {
            Result access = adminAccess.EnsureCanAccessOrganization(organizationId);
            if (access.IsFailure)
            {
                return Result<PageResponse<AdminStudentEnrolledCourseItem>>.Failure(access.Error);
            }
        }
        else if (!adminAccess.IsHqAdmin)
        {
            return Result<PageResponse<AdminStudentEnrolledCourseItem>>.Failure(
                CourseBillingErrors.OrganizationOutsideScope);
        }

        PageResponse<AdminStudentEnrolledCourseProjection> page = await reader.ListAsync(
            query.PersonId,
            query.Page,
            query.PageSize,
            cancellationToken);

        AdminStudentEnrolledCourseItem[] items = page.Items
            .Select(x => new AdminStudentEnrolledCourseItem(
                x.CourseId,
                x.CourseName,
                ToStatusLabel(x.EnrollmentStatusCode),
                x.EnrolledAtUtc,
                x.Fee,
                x.FasApplied,
                x.Paid,
                x.Outstanding))
            .ToArray();

        return Result<PageResponse<AdminStudentEnrolledCourseItem>>.Success(
            new PageResponse<AdminStudentEnrolledCourseItem>(
                items,
                page.Page,
                page.PageSize,
                page.TotalCount));
    }

    private static string ToStatusLabel(string statusCode)
        => statusCode.Trim().ToUpperInvariant() switch
        {
            CourseEnrollmentStatusCodes.PendingPayment => "Active",
            CourseEnrollmentStatusCodes.Completed => "Completed",
            CourseEnrollmentStatusCodes.Cancelled => "Dropped Out",
            CourseEnrollmentStatusCodes.Exited => "Dropped Out",
            _ => statusCode
        };
}
