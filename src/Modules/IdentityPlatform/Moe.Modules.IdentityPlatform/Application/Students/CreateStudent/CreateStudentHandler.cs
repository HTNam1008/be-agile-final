using System.Security.Cryptography;
using System.Text;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.Application.Organizations;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.IdentityPlatform.IGateway.Accounts;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application.Students.CreateStudent;

internal sealed class CreateStudentHandler(
    ICurrentUser currentUser,
    IAdminAccessControl adminAccess,
    IClock clock,
    IOrganizationUnitRepository organizations,
    IStudentOnboardingRepository students,
    IEducationAccountProvisioningGateway educationAccounts)
    : ICommandHandler<CreateStudentCommand, CreateStudentResponse>
{
    public async Task<Result<CreateStudentResponse>> Handle(
        CreateStudentCommand command,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserAccountId is not long actorUserAccountId)
        {
            return Result<CreateStudentResponse>.Failure(IdentityErrors.AuthenticatedAdminRequired);
        }

        Result<OrganizationUnitSummary> schoolResult = await ResolveSchoolAsync(command.SchoolName, cancellationToken);

        if (schoolResult.IsFailure)
        {
            return Result<CreateStudentResponse>.Failure(schoolResult.Error);
        }

        OrganizationUnitSummary school = schoolResult.Value;
        byte[] identityNumberHash = HashIdentifier(command.IdentityNumber);
        string normalizedStudentNumber = command.StudentNumber.Trim().ToUpperInvariant();

        if (await students.IdentityNumberExistsAsync(identityNumberHash, cancellationToken))
        {
            return Result<CreateStudentResponse>.Failure(IdentityErrors.StudentIdentityAlreadyExists);
        }

        if (await students.StudentNumberExistsAsync(normalizedStudentNumber, cancellationToken))
        {
            return Result<CreateStudentResponse>.Failure(IdentityErrors.StudentNumberAlreadyExists);
        }

        DateTime utcNow = clock.UtcNow.UtcDateTime;
        DateOnly startDate = command.StartDate ?? DateOnly.FromDateTime(utcNow);
        string normalizedIdentityNumber = command.IdentityNumber.Trim().ToUpperInvariant();

        Person person = Person.CreateStudent(
            $"MOE-STUDENT-{Guid.NewGuid():N}",
            normalizedIdentityNumber,
            command.FullName,
            command.DateOfBirth,
            command.NationalityCode,
            command.CitizenshipStatusCode,
            command.Email,
            command.Mobile,
            command.Address,
            utcNow);

        long personId = await students.AddPersonAsync(person, cancellationToken);

        PersonIdentifier identityNumber = PersonIdentifier.CreateIdentityNumber(
            personId,
            identityNumberHash,
            normalizedIdentityNumber,
            utcNow);

        SchoolEnrollment enrollment = new(
            personId,
            school.OrganizationUnitId,
            normalizedStudentNumber,
            command.AcademicYear,
            command.LevelCode,
            command.ClassCode,
            startDate,
            utcNow);

        await students.AddStudentIdentityAndEnrollmentAsync(identityNumber, enrollment, cancellationToken);

        Result<EducationAccountProvisioningResult> accountResult = command.IsAccountHolder
            ? await CreateEducationAccountAsync(personId, actorUserAccountId, utcNow, cancellationToken)
            : Result<EducationAccountProvisioningResult>.Success(new EducationAccountProvisioningResult(null, null, false));

        if (accountResult.IsFailure)
        {
            return Result<CreateStudentResponse>.Failure(accountResult.Error);
        }

        EducationAccountProvisioningResult account = accountResult.Value;

        return Result<CreateStudentResponse>.Success(new CreateStudentResponse(
            personId,
            school.OrganizationUnitId,
            school.UnitName,
            normalizedStudentNumber,
            command.FullName.Trim(),
            account.IsAccountHolder,
            account.EducationAccountId,
            account.AccountNumber));
    }

    private async Task<Result<OrganizationUnitSummary>> ResolveSchoolAsync(
        string? schoolName,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(schoolName))
        {
            OrganizationUnitSummary? school = await organizations.FindActiveSchoolByNameAsync(
                schoolName,
                cancellationToken);

            if (school is null)
            {
                return Result<OrganizationUnitSummary>.Failure(IdentityErrors.OrganizationUnitNotFound);
            }

            Result access = adminAccess.EnsureCanAccessOrganization(school.OrganizationUnitId);
            return access.IsFailure
                ? Result<OrganizationUnitSummary>.Failure(IdentityErrors.SchoolOutsideScope)
                : Result<OrganizationUnitSummary>.Success(school);
        }

        if (adminAccess.IsHqAdmin)
        {
            return Result<OrganizationUnitSummary>.Failure(IdentityErrors.SchoolNameRequired);
        }

        long[] scopedSchools = adminAccess.ScopedOrganizationIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();

        if (scopedSchools.Length != 1)
        {
            return Result<OrganizationUnitSummary>.Failure(
                scopedSchools.Length == 0
                    ? IdentityErrors.SchoolNameRequired
                    : IdentityErrors.SchoolScopeAmbiguous);
        }

        OrganizationUnitSummary? scopedSchool = await organizations.FindActiveSchoolByIdAsync(
            scopedSchools[0],
            cancellationToken);

        return scopedSchool is null
            ? Result<OrganizationUnitSummary>.Failure(IdentityErrors.OrganizationUnitNotFound)
            : Result<OrganizationUnitSummary>.Success(scopedSchool);
    }

    private async Task<Result<EducationAccountProvisioningResult>> CreateEducationAccountAsync(
        long personId,
        long actorUserAccountId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        try
        {
            EducationAccountProvisioningResult result = await educationAccounts.EnsureAccountForStudentAsync(
                personId,
                actorUserAccountId,
                new DateTimeOffset(utcNow, TimeSpan.Zero),
                cancellationToken);

            return Result<EducationAccountProvisioningResult>.Success(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<EducationAccountProvisioningResult>.Failure(IdentityErrors.StudentAccountCreateFailed);
        }
    }

    private static byte[] HashIdentifier(string value)
    {
        string normalized = value.Trim().ToUpperInvariant();
        return SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
    }
}
