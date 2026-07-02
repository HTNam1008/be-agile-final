using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.IdentityPlatform.Application.Students.CreateStudent;

public sealed record CreateStudentCommand(
    string? SchoolName,
    long? OrganizationId,
    string IdentityNumber,
    string FullName,
    DateOnly DateOfBirth,
    string NationalityCode,
    string? CitizenshipStatusCode,
    string StudentNumber,
    string AcademicYear,
    string LevelCode,
    string? ClassCode,
    DateOnly? StartDate,
    string? Email,
    string? ContactNumber,
    string? Address,
    [property: Obsolete("Manual student creation now always creates an education account. This field is accepted for backward compatibility and ignored.")]
    bool IsAccountHolder) : ICommand<CreateStudentResponse>;
