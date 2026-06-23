using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.IdentityPlatform.Application.Students.CreateStudent;

public sealed record CreateStudentCommand(
    string? SchoolName,
    string IdentityNumber,
    string FullName,
    DateOnly DateOfBirth,
    string NationalityCode,
    string CitizenshipStatusCode,
    string StudentNumber,
    string AcademicYear,
    string LevelCode,
    string ClassCode,
    DateOnly? StartDate,
    string? Email,
    string? Mobile,
    string? Address,
    bool IsAccountHolder) : ICommand<CreateStudentResponse>;
