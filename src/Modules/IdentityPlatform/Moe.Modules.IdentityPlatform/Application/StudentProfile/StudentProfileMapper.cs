using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;

namespace Moe.Modules.IdentityPlatform.Application.StudentProfile;

internal static class StudentProfileMapper
{
    public static StudentProfileResponse ToResponse(
        UserAccount account,
        StudentProfileSummary profile,
        DateOnly today,
        ICurrentUser currentUser)
    {
        int age = CalculateAge(profile.DateOfBirth, today);

        return new StudentProfileResponse(
            account.Id,
            profile.PersonId,
            account.DisplayNameSnapshot ?? profile.OfficialFullName,
            account.ContactEmail,
            account.ContactMobile,
            account.RoleCode,
            account.IdentityProviderCode,
            account.ExternalIssuer,
            account.ExternalSubjectId,
            profile.IdentityNumberMasked,
            profile.OfficialFullName,
            profile.DateOfBirth,
            age,
            age >= 16,
            profile.NationalityCode,
            profile.CitizenshipStatusCode,
            profile.OfficialEmail,
            profile.PreferredEmail,
            profile.OfficialMobile,
            profile.PreferredMobile,
            profile.OfficialAddress,
            profile.PreferredAddress,
            profile.UpdatedAtUtc,
            new StudentEnrollmentProfileResponse(
                profile.SchoolEnrollmentId,
                profile.SchoolOrganizationId,
                profile.SchoolOrganizationCode,
                profile.SchoolOrganizationName,
                profile.StudentNumber,
                profile.AcademicYear,
                profile.LevelCode,
                profile.ClassCode,
                profile.SchoolingStatusCode,
                profile.EnrollmentStartDate,
                profile.EnrollmentEndDate),
            account.AccountStatusCode,
            account.FirstLoginAtUtc,
            account.LastLoginAtUtc,
            account.CreatedAtUtc,
            account.UpdatedAtUtc,
            currentUser.OrganizationUnitIds,
            currentUser.Roles,
            currentUser.Permissions);
    }

    private static int CalculateAge(DateOnly dateOfBirth, DateOnly today)
    {
        int age = today.Year - dateOfBirth.Year;

        if (dateOfBirth > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }
}
