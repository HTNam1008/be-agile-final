using Moe.Modules.IdentityPlatform.IGateway.Accounts;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;

namespace Moe.Modules.IdentityPlatform.Application.AdminAccountDetails;

internal static class AdminAccountDetailsMapper
{
    private const string NoAccount = "NO_ACCOUNT";

    public static AdminAccountDetailsResponse ToResponse(
        AdminAccountDetailsProfile profile,
        EducationAccountLookupSummary account)
        => new(
            profile.PersonId,
            profile.PersonStatusCode,
            profile.UserAccountId,
            profile.UserAccountStatusCode,
            account.EducationAccountId,
            account.AccountNumber,
            profile.IdentityNumberMasked,
            profile.OfficialFullName,
            profile.DateOfBirth,
            profile.NationalityCode,
            profile.MailingAddress,
            profile.ResidentialAddress,
            profile.Email,
            profile.ContactNumber,
            profile.SchoolOrganizationId,
            profile.SchoolOrganizationCode,
            profile.SchoolOrganizationName,
            profile.AcademicYear,
            profile.LevelCode,
            profile.ClassCode,
            account.AccountStatusCode,
            account.CurrentBalance,
            profile.UpdatedAtUtc);

    public static AdminAccountDetailsResponse ToNoAccountResponse(AdminAccountDetailsProfile profile)
        => new(
            profile.PersonId,
            profile.PersonStatusCode,
            profile.UserAccountId,
            profile.UserAccountStatusCode,
            null,
            null,
            profile.IdentityNumberMasked,
            profile.OfficialFullName,
            profile.DateOfBirth,
            profile.NationalityCode,
            profile.MailingAddress,
            profile.ResidentialAddress,
            profile.Email,
            profile.ContactNumber,
            profile.SchoolOrganizationId,
            profile.SchoolOrganizationCode,
            profile.SchoolOrganizationName,
            profile.AcademicYear,
            profile.LevelCode,
            profile.ClassCode,
            NoAccount,
            null,
            profile.UpdatedAtUtc);
}
