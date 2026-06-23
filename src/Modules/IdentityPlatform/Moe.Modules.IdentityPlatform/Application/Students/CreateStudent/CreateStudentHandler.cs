using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Persistence;
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
    IEducationAccountProvisioningGateway educationAccounts,
    IAuditService auditService,
    IUnitOfWork unitOfWork,
    ITransactionalExecutor transactions)
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

        Result<OrganizationUnitSummary> schoolResult = await ResolveSchoolAsync(
            command.OrganizationId,
            command.SchoolName,
            cancellationToken);

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

        return await transactions.ExecuteAsync(
            ct => CreateStudentAsync(command, school, actorUserAccountId, identityNumberHash, normalizedStudentNumber, ct),
            cancellationToken);
    }

    private async Task<Result<CreateStudentResponse>> CreateStudentAsync(
        CreateStudentCommand command,
        OrganizationUnitSummary school,
        long actorUserAccountId,
        byte[] identityNumberHash,
        string normalizedStudentNumber,
        CancellationToken cancellationToken)
    {
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

        await students.AddStudentIdentityAndEnrollmentAsync(identityNumber, enrollment, cancellationToken, saveChanges: false);

        Result<EducationAccountProvisioningResult> accountResult =
            await CreateEducationAccountAsync(personId, actorUserAccountId, utcNow, cancellationToken);

        if (accountResult.IsFailure)
        {
            return Result<CreateStudentResponse>.Failure(accountResult.Error);
        }

        EducationAccountProvisioningResult account = accountResult.Value;
        string detailsJson = JsonSerializer.Serialize(new
        {
            personId,
            accountNumber = account.AccountNumber,
            openedByUserId = actorUserAccountId
        });

        await auditService.RecordAsync(
            AuditActionCodes.EducationAccountCreatedManually,
            "EducationAccount",
            account.EducationAccountId!.Value.ToString(),
            detailsJson,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

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
        long? organizationId,
        string? schoolName,
        CancellationToken cancellationToken)
    {
        if (organizationId is long requestedOrganizationId
            && !string.IsNullOrWhiteSpace(schoolName))
        {
            Result<OrganizationUnitSummary> schoolById = await ResolveSchoolByIdAsync(
                requestedOrganizationId,
                cancellationToken);

            if (schoolById.IsFailure)
            {
                return schoolById;
            }

            Result<OrganizationUnitSummary> schoolByName = await ResolveSchoolByNameAsync(
                schoolName,
                cancellationToken);

            if (schoolByName.IsFailure)
            {
                return schoolByName;
            }

            if (schoolById.Value.OrganizationUnitId != schoolByName.Value.OrganizationUnitId)
            {
                return Result<OrganizationUnitSummary>.Failure(IdentityErrors.SchoolIdentifiersConflict);
            }

            return EnsureCanAccess(schoolById.Value);
        }

        if (organizationId is long organizationIdOnly)
        {
            Result<OrganizationUnitSummary> school = await ResolveSchoolByIdAsync(
                organizationIdOnly,
                cancellationToken);

            return school.IsFailure
                ? school
                : EnsureCanAccess(school.Value);
        }

        if (!string.IsNullOrWhiteSpace(schoolName))
        {
            Result<OrganizationUnitSummary> school = await ResolveSchoolByNameAsync(
                schoolName,
                cancellationToken);

            return school.IsFailure
                ? school
                : EnsureCanAccess(school.Value);
        }

        if (adminAccess.IsHqAdmin)
        {
            return Result<OrganizationUnitSummary>.Failure(IdentityErrors.SchoolRequired);
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
            : EnsureCanAccess(scopedSchool);
    }

    private async Task<Result<OrganizationUnitSummary>> ResolveSchoolByIdAsync(
        long organizationId,
        CancellationToken cancellationToken)
    {
        OrganizationUnitSummary? school = await organizations.FindActiveSchoolByIdAsync(
            organizationId,
            cancellationToken);

        return school is null
            ? Result<OrganizationUnitSummary>.Failure(IdentityErrors.OrganizationUnitNotFound)
            : Result<OrganizationUnitSummary>.Success(school);
    }

    private async Task<Result<OrganizationUnitSummary>> ResolveSchoolByNameAsync(
        string schoolName,
        CancellationToken cancellationToken)
    {
        OrganizationUnitSummary? school = await organizations.FindActiveSchoolByNameAsync(
            schoolName,
            cancellationToken);

        return school is null
            ? Result<OrganizationUnitSummary>.Failure(IdentityErrors.OrganizationUnitNotFound)
            : Result<OrganizationUnitSummary>.Success(school);
    }

    private Result<OrganizationUnitSummary> EnsureCanAccess(OrganizationUnitSummary school)
    {
        Result access = adminAccess.EnsureCanAccessOrganization(school.OrganizationUnitId);
        return access.IsFailure
            ? Result<OrganizationUnitSummary>.Failure(IdentityErrors.SchoolOutsideScope)
            : Result<OrganizationUnitSummary>.Success(school);
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
                cancellationToken,
                saveChanges: false);

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
