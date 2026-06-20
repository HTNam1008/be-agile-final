using Moe.Application.Abstractions.Messaging;
using Moe.Modules.CourseBilling.Contracts.Enrollments;

namespace Moe.Modules.CourseBilling.Application.Enrollments.SelfJoinCourse;

public sealed record SelfJoinCourseCommand(long CourseId) : ICommand<CourseEnrollmentResponse>;
