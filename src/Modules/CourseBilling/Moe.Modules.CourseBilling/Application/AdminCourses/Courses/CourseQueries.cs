using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.CourseBilling.Contracts.AdminCourses;

namespace Moe.Modules.CourseBilling.Application.AdminCourses.Courses;

public sealed record ListCoursesQuery(CourseQueryRequest Request) : IQuery<PageResponse<CourseSummaryDto>>;
public sealed record GetCourseQuery(long CourseId) : IQuery<CourseDetailDto>;
public sealed record PreviewCourseQuery(long CourseId) : IQuery<CoursePreviewDto>;
