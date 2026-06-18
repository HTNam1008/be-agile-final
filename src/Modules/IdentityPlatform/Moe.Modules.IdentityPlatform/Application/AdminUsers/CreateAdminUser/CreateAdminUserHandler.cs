using Microsoft.Extensions.Options;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.Application;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.IGateway.Admin;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.Modules.IdentityPlatform.Infrastructure.EntraWorkforce;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application.AdminUsers.CreateAdminUser;

internal sealed class CreateAdminUserHandler(
    ICurrentUser currentUser,
    IClock clock,
    IEntraWorkforceDirectoryClient directoryClient,
    IUserAccountRepository userAccounts,
    IAdminUserRepository adminUsers,
    IOptions<EntraWorkforceDirectoryOptions> options)
    : ICommandHandler<CreateAdminUserCommand, CreateAdminUserResponse>
{
    public async Task<Result<CreateAdminUserResponse>> Handle(
        CreateAdminUserCommand command,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserAccountId is not long actorUserAccountId)
        {
            return Result<CreateAdminUserResponse>.Failure(IdentityErrors.AuthenticatedAdminRequired);
        }

        DateTime utcNow = clock.UtcNow.UtcDateTime;
        string email = command.Email.Trim();
        string normalizedEmail = NormalizeEmail(command.Email);
        string adminRoleCode = command.RoleCode.Trim().ToUpperInvariant();

        if (adminRoleCode is not (RoleCodes.SystemAdmin or RoleCodes.SchoolAdmin))
        {
            return Result<CreateAdminUserResponse>.Failure(IdentityErrors.InvalidAdminRole);
        }

        if (!currentUser.Roles.Contains(RoleCodes.SystemAdmin))
        {
            return Result<CreateAdminUserResponse>.Failure(IdentityErrors.SystemAdminRequired);
        }

        UserAccount? actor = await userAccounts.FindByIdAsync(actorUserAccountId, cancellationToken);

        if (actor is null || actor.PortalAccessCode != PortalAccessCodes.Admin)
        {
            return Result<CreateAdminUserResponse>.Failure(IdentityErrors.SystemAdminRequired);
        }

        if (actor.LoginEmailNormalized == normalizedEmail)
        {
            return Result<CreateAdminUserResponse>.Failure(IdentityErrors.AdminCannotCreateOwnAccount);
        }

        if (await userAccounts.ExistsAdminByEmailAsync(normalizedEmail, cancellationToken))
        {
            return Result<CreateAdminUserResponse>.Failure(IdentityErrors.AdminAccountAlreadyExists);
        }

        string? organizationType = await adminUsers.GetActiveOrganizationUnitTypeAsync(
            command.InitialOrganizationUnitId,
            utcNow,
            cancellationToken);

        if (organizationType is null)
        {
            return Result<CreateAdminUserResponse>.Failure(IdentityErrors.OrganizationUnitNotFound);
        }

        if (!IsValidRoleOrganizationPair(adminRoleCode, command.InitialOrganizationUnitId, organizationType))
        {
            return Result<CreateAdminUserResponse>.Failure(IdentityErrors.InvalidAdminOrganizationScope);
        }

        if (!await adminUsers.HasActiveRolePermissionsAsync(adminRoleCode, utcNow, cancellationToken))
        {
            return Result<CreateAdminUserResponse>.Failure(IdentityErrors.RoleNotConfigured);
        }

        CreateEntraUserGatewayResult createdUser;

        try
        {
            createdUser = await directoryClient.CreateUserAsync(
                new CreateEntraUserGatewayRequest(
                    email,
                    command.DisplayName.Trim(),
                    command.MailNickname.Trim(),
                    command.TemporaryPassword,
                    command.AccountEnabled),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<CreateAdminUserResponse>.Failure(IdentityErrors.AdminDirectoryCreateFailed(ex.Message));
        }

        try
        {
            UserAccount account = UserAccount.CreateAdmin(
                options.Value.EffectiveIssuer,
                createdUser.ExternalObjectId,
                options.Value.TenantId,
                createdUser.ExternalObjectId,
                createdUser.UserPrincipalName,
                createdUser.DisplayName,
                adminRoleCode,
                command.InitialOrganizationUnitId,
                actorUserAccountId,
                utcNow);

            CreatedAdminLocalAccount localAccount = await adminUsers.CreateAdminWithInitialScopeAsync(
                account,
                command.InitialOrganizationUnitId,
                adminRoleCode,
                actorUserAccountId,
                utcNow,
                cancellationToken);

            CreateAdminUserResponse response = new(
                localAccount.UserAccountId,
                createdUser.ExternalObjectId,
                createdUser.UserPrincipalName,
                createdUser.DisplayName,
                localAccount.AccountStatusCode,
                localAccount.InitialScopeId,
                localAccount.OrganizationUnitId,
                localAccount.RoleCode,
                actorUserAccountId);

            return Result<CreateAdminUserResponse>.Success(response);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            try
            {
                await directoryClient.DeleteUserAsync(createdUser.ExternalObjectId, CancellationToken.None);
            }
            catch
            {
                // Keep the original persistence failure visible to the caller.
            }

            return Result<CreateAdminUserResponse>.Failure(IdentityErrors.AdminLocalAccountCreateFailed(ex.Message));
        }
    }

    private static string NormalizeEmail(string email)
        => email.Trim().ToUpperInvariant();

    private static bool IsValidRoleOrganizationPair(string roleCode, long organizationUnitId, string organizationType)
    {
        return roleCode switch
        {
            RoleCodes.SystemAdmin => organizationUnitId == OrganizationUnitCodes.MoeHeadquartersId
                && organizationType == "HQ",
            RoleCodes.SchoolAdmin => organizationType == "SCHOOL",
            _ => false
        };
    }
}
