using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.CourseBilling.Contracts.AdminCourses;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.AdminCourses.Courses;

internal sealed class ListCoursesQueryHandler(AdminCourseAccess access)
    : IQueryHandler<ListCoursesQuery, PageResponse<CourseSummaryDto>>
{
    public async Task<Result<PageResponse<CourseSummaryDto>>> Handle(
        ListCoursesQuery query,
        CancellationToken cancellationToken)
    {
        CourseQueryRequest request = query.Request;

        Result admin = access.RequireAdmin();
        if (admin.IsFailure)
        {
            return Result<PageResponse<CourseSummaryDto>>.Failure(admin.Error);
        }

        if (request.StartDate.HasValue && request.EndDate.HasValue && request.StartDate.Value > request.EndDate.Value)
        {
            return Result<PageResponse<CourseSummaryDto>>.Failure(
                new Error("COURSE.INVALID_FILTER_DATE_RANGE", "Filter start date cannot be after filter end date."));
        }

        if (!string.IsNullOrWhiteSpace(request.StatusCode)
            && request.StatusCode.Trim().ToUpperInvariant() is not (
                CourseStatusCodes.Draft
                or CourseStatusCodes.Published
                or CourseStatusCodes.Disabled))
        {
            return Result<PageResponse<CourseSummaryDto>>.Failure(
                new Error("COURSE.INVALID_STATUS_FILTER", "Course status must be DRAFT, PUBLISHED or DISABLED."));
        }

        if (request.OrganizationId.HasValue && !access.CanAccessOrganization(request.OrganizationId.Value))
        {
            return Result<PageResponse<CourseSummaryDto>>.Failure(access.OrganizationForbidden());
        }

        IReadOnlyCollection<long>? scope = access.OrganizationScope;
        await access.DisableEndedCoursesOnAccessAsync(scope, cancellationToken);

        PageResponse<CourseSummaryDto> courses = await access.Courses.ListCoursesAsync(
            request,
            scope,
            cancellationToken);

        return Result<PageResponse<CourseSummaryDto>>.Success(courses);
    }
}

internal sealed class GetCourseQueryHandler(AdminCourseAccess access)
    : IQueryHandler<GetCourseQuery, CourseDetailDto>
{
    public async Task<Result<CourseDetailDto>> Handle(GetCourseQuery query, CancellationToken cancellationToken)
    {
        Result admin = access.RequireAdmin();
        if (admin.IsFailure)
        {
            return Result<CourseDetailDto>.Failure(admin.Error);
        }

        await access.DisableEndedCoursesOnAccessAsync(access.OrganizationScope, cancellationToken);

        CourseAggregate? aggregate = await access.Courses.GetCourseAggregateAsync(query.CourseId, cancellationToken);
        if (aggregate is null)
        {
            return Result<CourseDetailDto>.Failure(CourseErrors.CourseNotFound);
        }

        if (!access.CanAccessOrganization(aggregate.Course.OrganizationId))
        {
            return Result<CourseDetailDto>.Failure(access.OrganizationForbidden());
        }

        return Result<CourseDetailDto>.Success(CourseMapper.ToDetail(aggregate));
    }
}

internal sealed class PreviewCourseQueryHandler(AdminCourseAccess access)
    : IQueryHandler<PreviewCourseQuery, CoursePreviewDto>
{
    public async Task<Result<CoursePreviewDto>> Handle(PreviewCourseQuery query, CancellationToken cancellationToken)
    {
        Result admin = access.RequireAdmin();
        if (admin.IsFailure)
        {
            return Result<CoursePreviewDto>.Failure(admin.Error);
        }

        await access.DisableEndedCoursesOnAccessAsync(access.OrganizationScope, cancellationToken);

        CourseAggregate? aggregate = await access.Courses.GetCourseAggregateAsync(query.CourseId, cancellationToken);
        if (aggregate is null)
        {
            return Result<CoursePreviewDto>.Failure(CourseErrors.CourseNotFound);
        }

        if (!access.CanAccessOrganization(aggregate.Course.OrganizationId))
        {
            return Result<CoursePreviewDto>.Failure(access.OrganizationForbidden());
        }

        CourseDetailDto detail = CourseMapper.ToDetail(aggregate);
        CourseFeeDto[] activeFees = detail.Fees.Where(x => x.IsActive).ToArray();

        return Result<CoursePreviewDto>.Success(new CoursePreviewDto(
            detail,
            activeFees.Sum(x => x.FeeValue),
            activeFees.Length));
    }
}
