using FluentAssertions;
using Moe.Modules.IdentityPlatform.Api.Admin;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Application.Students.CreateStudent;
using Xunit;

namespace Moe.IdentityPlatform.UnitTests.Application.Students;

public sealed class CreateStudentValidatorTests
{
    [Fact]
    public void ApplicationValidator_AllowsMissingCitizenshipStatusCode()
    {
        CreateStudentValidator validator = new();

        var result = validator.Validate(ValidCommand() with
        {
            CitizenshipStatusCode = null
        });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ApiValidator_AllowsMissingCitizenshipStatusCode()
    {
        CreateStudentRequestValidator validator = new();

        var result = validator.Validate(ValidRequest() with
        {
            CitizenshipStatusCode = null
        });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void PersonCreateStudent_PreservesMissingCitizenshipStatusCodeAsNull()
    {
        Person person = Person.CreateStudent(
            externalReference: "MOE-STUDENT-001",
            identityNumberMasked: "S1234567A",
            fullName: "Valid Student",
            dateOfBirth: new DateOnly(2008, 1, 1),
            nationalityCode: "SG",
            citizenshipStatusCode: " ",
            email: null,
            mobile: null,
            address: null,
            utcNow: new DateTime(2026, 6, 26, 0, 0, 0, DateTimeKind.Utc));

        person.CitizenshipStatusCode.Should().BeNull();
    }

    private static CreateStudentCommand ValidCommand()
        => new(
            SchoolName: null,
            OrganizationId: 1,
            IdentityNumber: "S1234567A",
            FullName: "Valid Student",
            DateOfBirth: new DateOnly(2008, 1, 1),
            NationalityCode: "SG",
            CitizenshipStatusCode: "CITIZEN",
            StudentNumber: "STU-001",
            AcademicYear: "2026",
            LevelCode: "SEC_4",
            ClassCode: "4A",
            StartDate: new DateOnly(2026, 1, 1),
            Email: "student@example.sg",
            Mobile: "+6591234567",
            Address: "Valid address",
            IsAccountHolder: true);

    private static CreateStudentRequest ValidRequest()
        => new(
            SchoolName: null,
            OrganizationId: 1,
            IdentityNumber: "S1234567A",
            FullName: "Valid Student",
            DateOfBirth: new DateOnly(2008, 1, 1),
            NationalityCode: "SG",
            CitizenshipStatusCode: "CITIZEN",
            StudentNumber: "STU-001",
            AcademicYear: "2026",
            LevelCode: "SEC_4",
            ClassCode: "4A",
            StartDate: new DateOnly(2026, 1, 1),
            Email: "student@example.sg",
            Mobile: "+6591234567",
            Address: "Valid address",
            IsAccountHolder: true);
}
