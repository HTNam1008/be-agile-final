using Moe.Application.Abstractions.Messaging;
using Moe.Modules.CourseBilling.Contracts.AdminCourses;

namespace Moe.Modules.CourseBilling.Application.AdminCourses.Materials;

public sealed record ListCourseMaterialsQuery(long CourseId) : IQuery<IReadOnlyList<CourseMaterialDto>>;
