using Moe.Application.Abstractions.Messaging;
using Moe.Modules.CourseBilling.Contracts.AdminCourses;

namespace Moe.Modules.CourseBilling.Application.AdminCourses.Materials;

public sealed record AddCourseMaterialCommand(long CourseId, CreateCourseMaterialRequest Request) : ICommand<CourseMaterialDto>;
public sealed record CopyCourseMaterialsCommand(long CourseId, long SourceCourseId) : ICommand<IReadOnlyList<CourseMaterialDto>>;
public sealed record UpdateCourseMaterialCommand(long CourseId, long CourseMaterialId, UpdateCourseMaterialRequest Request) : ICommand<CourseMaterialDto>;
public sealed record ReplaceCourseMaterialFileCommand(long CourseId, long CourseMaterialId, ReplaceCourseMaterialFileRequest Request) : ICommand<CourseMaterialDto>;
public sealed record DeleteCourseMaterialCommand(long CourseId, long CourseMaterialId) : ICommand<CourseMaterialDto>;
