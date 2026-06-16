using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.Application;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.IGateway.Accounts;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application.ExternalProvisioning.ProvisionStudentSingpassAccount;

internal sealed class ProvisionStudentSingpassAccountHandler(
    ICurrentUser currentUser,
    IUserAccountRepository userAccounts,
    IIdentityProvisioningRequestRepository provisioningRequests,
    IStudentSingpassProvisioningRepository studentProvisioning,
    IEducationAccountProvisioningGateway educationAccountProvisioning,
    IClock clock) : ICommandHandler<ProvisionStudentSingpassAccountCommand, ProvisionStudentSingpassAccountResponse>
{
    public async Task<Result<ProvisionStudentSingpassAccountResponse>> Handle(
        ProvisionStudentSingpassAccountCommand command,
        CancellationToken cancellationToken)
    {
        long? actorId = currentUser.UserAccountId;

        if (actorId is null)
        {
            return Result<ProvisionStudentSingpassAccountResponse>.Failure(IdentityErrors.AuthenticatedAdminRequired);
        }

        Person? person = await studentProvisioning.FindPersonAsync(command.PersonId, cancellationToken);

        if (person is null)
        {
            return Result<ProvisionStudentSingpassAccountResponse>.Failure(IdentityErrors.PersonNotFound);
        }

        IdentityProvisioningRequest? existingRequest = await provisioningRequests.FindByIdempotencyKeyAsync(
            command.IdempotencyKey,
            cancellationToken);

        if (existingRequest is not null)
        {
            UserAccount? existingAccount = await studentProvisioning.FindSingpassAccountForRequestAsync(
                existingRequest,
                cancellationToken);

            if (existingAccount is null)
            {
                return Result<ProvisionStudentSingpassAccountResponse>.Failure(IdentityErrors.ActiveProvisioningRequestAlreadyExists);
            }

            await EnsureStudentRoleAndAccountAsync(person, existingAccount, actorId.Value, cancellationToken);

            ProvisionStudentSingpassAccountResponse existingResponse = new(
                existingRequest.Id,
                existingAccount.Id,
                existingRequest.ProvisioningStatusCode,
                existingAccount.AccountStatusCode);

            return Result<ProvisionStudentSingpassAccountResponse>.Success(existingResponse);
        }

        if (await userAccounts.ExistsSingpassForPersonAsync(command.PersonId, cancellationToken))
        {
            return Result<ProvisionStudentSingpassAccountResponse>.Failure(IdentityErrors.SingpassAccountAlreadyExists);
        }

        DateTime utcNow = clock.UtcNow.UtcDateTime;
        UserAccount account = UserAccount.CreateStudentSingpass(
            command.PersonId,
            command.ExternalIssuer,
            command.SingpassSubjectId,
            command.DisplayName,
            actorId.Value,
            utcNow);
        IdentityProvisioningRequest request = IdentityProvisioningRequest.CreateSingpassStudent(
            command.PersonId,
            command.ExternalIssuer,
            command.DisplayName,
            command.IdempotencyKey,
            actorId.Value,
            utcNow,
            Guid.NewGuid().ToString("N"));
        request.Complete(command.SingpassSubjectId, utcNow);

        await studentProvisioning.AddAccountAndRequestAsync(account, request, cancellationToken);

        await EnsureStudentRoleAndAccountAsync(person, account, actorId.Value, cancellationToken);

        ProvisionStudentSingpassAccountResponse response = new(
            request.Id,
            account.Id,
            request.ProvisioningStatusCode,
            account.AccountStatusCode);

        return Result<ProvisionStudentSingpassAccountResponse>.Success(response);
    }

    private static bool IsAccountHolderAge(DateOnly dateOfBirth, DateOnly today)
    {
        int age = today.Year - dateOfBirth.Year;

        if (dateOfBirth > today.AddYears(-age))
        {
            age--;
        }

        return age is >= 16 and <= 30;
    }

    private async Task EnsureStudentRoleAndAccountAsync(
        Person person,
        UserAccount account,
        long actorId,
        CancellationToken cancellationToken)
    {
        DateTime utcNow = clock.UtcNow.UtcDateTime;
        await studentProvisioning.EnsureActiveStudentScopeAsync(account.Id, actorId, utcNow, cancellationToken);

        if (IsAccountHolderAge(person.DateOfBirth, DateOnly.FromDateTime(utcNow)))
        {
            await educationAccountProvisioning.EnsureAccountForStudentAsync(
                person.Id,
                actorId,
                clock.UtcNow,
                cancellationToken);
        }
    }
}
