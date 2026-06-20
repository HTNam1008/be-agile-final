namespace Moe.Modules.IdentityPlatform.Application.Students.CreateStudent;

public sealed record CreateStudentResponse(
    long PersonId,
    long OrganizationId,
    string SchoolName,
    string StudentNumber,
    string DisplayName,
    bool IsAccountHolder,
    long? EducationAccountId,
    string? EducationAccountNumber);
