using Moe.Application.Abstractions.Messaging;
using Moe.Modules.CourseBilling.Contracts.AdminCourses;

namespace Moe.Modules.CourseBilling.Application.AdminCourses.Fees;

public sealed record AddCourseFeeCommand(long CourseId, CreateCourseFeeRequest Request) : ICommand<CourseFeeDto>;
public sealed record UpdateCourseFeeCommand(long CourseId, long CourseFeeId, UpdateCourseFeeRequest Request) : ICommand<CourseFeeDto>;
public sealed record DeleteCourseFeeCommand(long CourseId, long CourseFeeId) : ICommand<CourseFeeDto>;
