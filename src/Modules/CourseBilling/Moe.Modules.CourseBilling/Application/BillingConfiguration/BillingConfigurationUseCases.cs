using FluentValidation;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.Contracts.BillingConfiguration;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.BillingConfiguration;

internal sealed record GetBillingConfigurationQuery(long? OrganizationId) : IQuery<BillingConfigurationResponse>;

internal sealed record UpdateBillingConfigurationCommand(UpdateBillingConfigurationRequest Request)
    : ICommand<BillingConfigurationResponse>;

internal sealed class UpdateBillingConfigurationRequestValidator
    : AbstractValidator<UpdateBillingConfigurationRequest>
{
    public UpdateBillingConfigurationRequestValidator()
    {
        RuleFor(x => x.OrganizationId)
            .GreaterThan(0)
            .When(x => x.OrganizationId is not null);
        RuleFor(x => x.MaxDeferralCount).InclusiveBetween(0, 12);
        RuleFor(x => x.RejectionGracePeriodDays).InclusiveBetween(1, 90);
    }
}

internal sealed class GetBillingConfigurationHandler(
    IBillingPolicyRepository billingPolicies,
    IAdminAccessControl adminAccess)
    : IQueryHandler<GetBillingConfigurationQuery, BillingConfigurationResponse>
{
    public async Task<Result<BillingConfigurationResponse>> Handle(
        GetBillingConfigurationQuery query,
        CancellationToken cancellationToken)
    {
        Result<long> organizationResult = ResolveSchoolAdminOrganization(adminAccess, query.OrganizationId);
        if (organizationResult.IsFailure)
            return Result<BillingConfigurationResponse>.Failure(organizationResult.Error);

        OrganizationBillingConfiguration? configuration = await billingPolicies.FindConfigurationAsync(
            organizationResult.Value,
            cancellationToken);

        return Result<BillingConfigurationResponse>.Success(ToResponse(
            configuration,
            organizationResult.Value));
    }

    internal static Result<long> ResolveSchoolAdminOrganization(
        IAdminAccessControl adminAccess,
        long? requestedOrganizationId)
    {
        if (!adminAccess.IsSchoolAdmin)
            return Result<long>.Failure(CourseBillingErrors.SchoolAdminRequired);

        if (requestedOrganizationId is long organizationId)
        {
            Result scope = adminAccess.EnsureCanAccessOrganization(organizationId);
            return scope.IsFailure
                ? Result<long>.Failure(scope.Error)
                : Result<long>.Success(organizationId);
        }

        long[] scopedOrganizationIds = adminAccess.ScopedOrganizationIds.ToArray();
        return scopedOrganizationIds.Length switch
        {
            1 => Result<long>.Success(scopedOrganizationIds[0]),
            0 => Result<long>.Failure(CourseBillingErrors.OrganizationOutsideScope),
            _ => Result<long>.Failure(CourseBillingErrors.OrganizationRequired)
        };
    }

    internal static BillingConfigurationResponse ToResponse(
        OrganizationBillingConfiguration? configuration,
        long organizationId)
    {
        return configuration is null
            ? new BillingConfigurationResponse(
                organizationId,
                BillDeferralPolicy.DefaultMaxDeferralCount,
                BillDeferralPolicy.DefaultRejectionGracePeriodDays,
                0,
                DateTime.MinValue,
                DateTime.MinValue)
            : new BillingConfigurationResponse(
                configuration.OrganizationId,
                configuration.MaxDeferralCount,
                configuration.RejectionGracePeriodDays,
                configuration.UpdatedByLoginAccountId,
                configuration.CreatedAtUtc,
                configuration.UpdatedAtUtc);
    }
}

internal sealed class UpdateBillingConfigurationHandler(
    IBillingPolicyRepository billingPolicies,
    IAdminAccessControl adminAccess,
    ICurrentUser currentUser,
    IClock clock)
    : ICommandHandler<UpdateBillingConfigurationCommand, BillingConfigurationResponse>
{
    public async Task<Result<BillingConfigurationResponse>> Handle(
        UpdateBillingConfigurationCommand command,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserAccountId is not long actorId)
            return Result<BillingConfigurationResponse>.Failure(CourseBillingErrors.ActorRequired);

        Result<long> organizationResult = GetBillingConfigurationHandler.ResolveSchoolAdminOrganization(
            adminAccess,
            command.Request.OrganizationId);
        if (organizationResult.IsFailure)
            return Result<BillingConfigurationResponse>.Failure(organizationResult.Error);

        long organizationId = organizationResult.Value;
        DateTime now = clock.UtcNow.UtcDateTime;
        OrganizationBillingConfiguration? configuration = await billingPolicies.FindConfigurationAsync(
            organizationId,
            cancellationToken);

        if (configuration is null)
        {
            Result<OrganizationBillingConfiguration> createResult = OrganizationBillingConfiguration.Create(
                organizationId,
                command.Request.MaxDeferralCount,
                command.Request.RejectionGracePeriodDays,
                actorId,
                now);
            if (createResult.IsFailure)
                return Result<BillingConfigurationResponse>.Failure(createResult.Error);

            configuration = createResult.Value;
            await billingPolicies.AddConfigurationAsync(configuration, cancellationToken);
        }
        else
        {
            Result updateResult = configuration.Update(
                command.Request.MaxDeferralCount,
                command.Request.RejectionGracePeriodDays,
                actorId,
                now);
            if (updateResult.IsFailure)
                return Result<BillingConfigurationResponse>.Failure(updateResult.Error);
        }

        await billingPolicies.SaveChangesAsync(cancellationToken);
        return Result<BillingConfigurationResponse>.Success(
            GetBillingConfigurationHandler.ToResponse(configuration, organizationId));
    }
}
