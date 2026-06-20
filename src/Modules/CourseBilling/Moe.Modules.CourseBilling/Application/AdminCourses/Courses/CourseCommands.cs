using Moe.Application.Abstractions.Messaging;
using Moe.Modules.CourseBilling.Contracts.AdminCourses;

namespace Moe.Modules.CourseBilling.Application.AdminCourses.Courses;

public sealed record CreateCourseCommand(CreateCourseRequest Request) : ICommand<CourseDetailDto>;
public sealed record UpdateCourseCommand(long CourseId, UpdateCourseRequest Request) : ICommand<CourseDetailDto>;
public sealed record RemoveCourseCommand(long CourseId) : ICommand<long>;
public sealed record PublishCourseCommand(long CourseId) : ICommand<CourseDetailDto>;
public sealed record DisableCourseCommand(long CourseId, DisableCourseRequest Request) : ICommand<CourseDetailDto>;
public sealed record EnableCourseCommand(long CourseId) : ICommand<CourseDetailDto>;
