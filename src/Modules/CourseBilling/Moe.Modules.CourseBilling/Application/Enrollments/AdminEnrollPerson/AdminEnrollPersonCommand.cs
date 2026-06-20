using Moe.Application.Abstractions.Messaging;
using Moe.Modules.CourseBilling.Contracts.Enrollments;

namespace Moe.Modules.CourseBilling.Application.Enrollments.AdminEnrollPerson;

public sealed record AdminEnrollPersonCommand(
    long CourseId,
    long PersonId) : ICommand<CourseEnrollmentResponse>;
