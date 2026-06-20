using Moe.Application.Abstractions.Messaging;
using Moe.Modules.CourseBilling.Contracts.AdminCourses;

namespace Moe.Modules.CourseBilling.Application.AdminCourses.Enrollments;

public sealed record RemoveAdminCourseEnrollmentCommand(long CourseId, long CourseEnrollmentId) : ICommand<AdminCourseEnrollmentDto>;
