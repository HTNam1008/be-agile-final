using Moe.Application.Abstractions.Messaging;
using Moe.Modules.CourseBilling.Contracts.AdminCourses;

namespace Moe.Modules.CourseBilling.Application.AdminCourses.Fees;

public sealed record ListCourseFeesQuery(long CourseId) : IQuery<IReadOnlyList<CourseFeeDto>>;
