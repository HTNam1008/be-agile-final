using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.CourseBilling.Application.Enrollments.AdminEnrollPerson;

public sealed record AdminEnrollPersonCommand(
    long CourseId,
    long PersonId) : ICommand<CourseEnrollmentResponse>;
