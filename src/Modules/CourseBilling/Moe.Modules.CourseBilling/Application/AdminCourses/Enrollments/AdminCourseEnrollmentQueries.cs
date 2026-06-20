using Moe.Application.Abstractions.Messaging;
using Moe.Modules.CourseBilling.Contracts.AdminCourses;

namespace Moe.Modules.CourseBilling.Application.AdminCourses.Enrollments;

public sealed record ListAdminCourseEnrollmentsQuery(long CourseId) : IQuery<IReadOnlyList<AdminCourseEnrollmentDto>>;
