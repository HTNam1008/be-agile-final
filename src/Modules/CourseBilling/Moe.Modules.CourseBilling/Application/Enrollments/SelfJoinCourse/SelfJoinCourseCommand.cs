using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.CourseBilling.Application.Enrollments.SelfJoinCourse;

public sealed record SelfJoinCourseCommand(long CourseId) : ICommand<CourseEnrollmentResponse>;
