using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.IdentityPlatform.IGateway.Accounts;
using Moe.Modules.IdentityPlatform.IGateway.Authentication;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Authentication;

internal sealed class EServiceLoginResolver(
    MoeDbContext dbContext,
    IEducationAccountProvisioningGateway educationAccounts,
    IClock clock) : IEServiceLoginResolver
{
    public async Task<EServiceLoginResolution> ResolveAsync(
        SingpassLoginResult login,
        CancellationToken cancellationToken)
    {
        DateTime utcNow = clock.UtcNow.UtcDateTime;
        UserAccount? account = await FindAccountAsync(login, cancellationToken);

        if (account is null)
        {
            Person person = await ResolvePersonAsync(login, cancellationToken)
                ?? throw new UnauthorizedAccessException("No eligible MOE profile was found for this Singpass user.");

            await EnsureEligiblePersonAsync(person, cancellationToken);

            account = UserAccount.CreateStudentSingpass(
                person.Id,
                login.ExternalIssuer,
                login.ExternalSubjectId,
                login.DisplayName,
                createdByUserAccountId: null,
                utcNow);

            dbContext.Add(account);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            if (account.PortalAccessCode != PortalAccessCodes.EService
                || account.IdentityProviderCode != IdentityProviderCodes.Singpass
                || !account.PersonId.HasValue)
            {
                throw new UnauthorizedAccessException("This Singpass user is not eligible for the e-Service portal.");
            }

            Person person = await dbContext.Set<Person>()
                .SingleOrDefaultAsync(x => x.Id == account.PersonId.Value, cancellationToken)
                ?? throw new UnauthorizedAccessException("No eligible MOE profile was found for this Singpass user.");

            await EnsureEligiblePersonAsync(person, cancellationToken);
        }

        await EnsureStudentScopeAsync(account, utcNow, cancellationToken);
        account.RecordSuccessfulLogin(utcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new EServiceLoginResolution(
            account.Id,
            account.PersonId!.Value,
            account.DisplayNameSnapshot ?? login.DisplayName);
    }

    private Task<UserAccount?> FindAccountAsync(SingpassLoginResult login, CancellationToken cancellationToken)
    {
        return dbContext.Set<UserAccount>()
            .SingleOrDefaultAsync(x => x.IdentityProviderCode == IdentityProviderCodes.Singpass
                && x.ExternalIssuer == login.ExternalIssuer
                && x.ExternalSubjectId == login.ExternalSubjectId
                && (x.AccountStatusCode == UserAccountStatusCodes.Active
                    || x.AccountStatusCode == UserAccountStatusCodes.PendingFirstLogin),
                cancellationToken);
    }

    private async Task<Person?> ResolvePersonAsync(SingpassLoginResult login, CancellationToken cancellationToken)
    {
        byte[] subjectHash = HashIdentifier($"{login.ExternalIssuer}|{login.ExternalSubjectId}");
        byte[] identityNumberHash = HashIdentifier(login.IdentityNumber);

        long? personId = await dbContext.Set<PersonIdentifier>()
            .Where(x => x.IdentifierStatusCode == PersonIdentifierStatusCodes.Active
                && ((x.IdentifierTypeCode == PersonIdentifierTypeCodes.SingpassSubject
                        && x.IdentifierValueHash.SequenceEqual(subjectHash))
                    || (x.IdentifierTypeCode == PersonIdentifierTypeCodes.IdentityNumber
                        && x.IdentifierValueHash.SequenceEqual(identityNumberHash))))
            .Select(x => (long?)x.PersonId)
            .FirstOrDefaultAsync(cancellationToken);

        if (personId.HasValue)
        {
            return await dbContext.Set<Person>()
                .SingleOrDefaultAsync(x => x.Id == personId.Value, cancellationToken);
        }

        return await dbContext.Set<Person>()
            .SingleOrDefaultAsync(x => x.ExternalPersonReference == login.ExternalSubjectId, cancellationToken);
    }

    private async Task EnsureEligiblePersonAsync(Person person, CancellationToken cancellationToken)
    {
        if (person.PersonStatusCode != "ACTIVE")
        {
            throw new UnauthorizedAccessException("No eligible MOE profile was found for this Singpass user.");
        }

        DateOnly today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        bool activeStudent = await dbContext.Set<SchoolEnrollment>()
            .AnyAsync(x => x.PersonId == person.Id
                && x.SchoolingStatusCode == "ACTIVE"
                && x.StartDate <= today
                && (x.EndDate == null || x.EndDate >= today),
                cancellationToken);

        bool accountHolder = await educationAccounts.HasActiveAccountAsync(person.Id, cancellationToken);

        if (!activeStudent && !accountHolder)
        {
            throw new UnauthorizedAccessException("No eligible MOE profile was found for this Singpass user.");
        }
    }

    private async Task EnsureStudentScopeAsync(UserAccount account, DateTime utcNow, CancellationToken cancellationToken)
    {
        bool hasStudentScope = await dbContext.Set<UserAccessScope>()
            .AnyAsync(x => x.UserAccountId == account.Id
                && x.RoleCode == RoleCodes.Student
                && x.StatusCode == IamStatusCodes.Active
                && x.EffectiveFromUtc <= utcNow
                && (x.EffectiveToUtc == null || x.EffectiveToUtc > utcNow),
                cancellationToken);

        if (hasStudentScope)
        {
            return;
        }

        dbContext.Add(new UserAccessScope(
            account.Id,
            OrganizationUnitCodes.MoeHeadquartersId,
            RoleCodes.Student,
            account.Id,
            utcNow,
            utcNow));
    }

    private static byte[] HashIdentifier(string value)
    {
        string normalized = value.Trim().ToUpperInvariant();
        return SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
    }
}

internal static class PersonIdentifierTypeCodes
{
    public const string SingpassSubject = "SINGPASS_SUBJECT";
    public const string IdentityNumber = "IDENTITY_NUMBER";
}
