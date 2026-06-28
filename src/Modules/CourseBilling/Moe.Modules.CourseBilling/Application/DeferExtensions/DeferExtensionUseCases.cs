using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.CourseBilling.Contracts.DeferExtensions;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.DeferExtensions;

internal sealed record CreateDeferExtensionRequestCommand(long BillId)
    : ICommand<DeferExtensionRequestResponse>;

internal sealed record ListDeferExtensionRequestsQuery(DeferExtensionRequestQueryRequest Request)
    : IQuery<PageResponse<DeferExtensionRequestResponse>>;

internal sealed record ApproveDeferExtensionRequestCommand(long RequestId)
    : ICommand<DeferExtensionRequestResponse>;

internal sealed record RejectDeferExtensionRequestCommand(long RequestId)
    : ICommand<DeferExtensionRequestResponse>;

internal sealed class CreateDeferExtensionRequestHandler(
    IDeferExtensionRequestRepository requests,
    ICoursePaymentGateway billingPolicies,
    ICurrentUser currentUser,
    IClock clock)
    : ICommandHandler<CreateDeferExtensionRequestCommand, DeferExtensionRequestResponse>
{
    public async Task<Result<DeferExtensionRequestResponse>> Handle(
        CreateDeferExtensionRequestCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentUser.TryGetStudent(out long personId) || currentUser.UserAccountId is not long actorId)
            return Result<DeferExtensionRequestResponse>.Failure(CourseBillingErrors.StudentIdentityRequired);

        DeferExtensionBillSnapshot? bill = await requests.FindBillForStudentAsync(
            command.BillId,
            personId,
            cancellationToken);
        if (bill is null)
            return Result<DeferExtensionRequestResponse>.Failure(CourseBillingErrors.BillNotFound);

        BillingPolicySnapshot policy = await billingPolicies.GetBillingPolicyAsync(
            bill.OrganizationId,
            cancellationToken);
        if (bill.OutstandingAmount <= 0m ||
            bill.IsDeferExtensionGranted ||
            bill.DeferralCount < policy.MaxDeferralCount)
        {
            return Result<DeferExtensionRequestResponse>.Failure(CourseBillingErrors.DeferExtensionNotAllowed);
        }

        if (await requests.HasPendingRequestAsync(command.BillId, cancellationToken))
            return Result<DeferExtensionRequestResponse>.Failure(CourseBillingErrors.DeferExtensionDuplicatePending);

        DateTime now = clock.UtcNow.UtcDateTime;
        Result<DeferExtensionRequest> createResult = DeferExtensionRequest.Create(
            bill.BillId,
            bill.CourseEnrollmentId,
            bill.PersonId,
            bill.OrganizationId,
            actorId,
            now);
        if (createResult.IsFailure)
            return Result<DeferExtensionRequestResponse>.Failure(createResult.Error);

        await requests.AddAsync(createResult.Value, cancellationToken);
        await requests.SaveChangesAsync(cancellationToken);

        return Result<DeferExtensionRequestResponse>.Success(ToResponse(createResult.Value, bill));
    }

    internal static DeferExtensionRequestResponse ToResponse(
        DeferExtensionRequest request,
        DeferExtensionBillSnapshot bill)
        => new(
            request.Id,
            request.BillId,
            request.CourseEnrollmentId,
            request.PersonId,
            request.OrganizationId,
            request.StatusCode,
            request.RequestedAtUtc,
            request.RequestedByLoginAccountId,
            request.ReviewedAtUtc,
            request.ReviewedByLoginAccountId,
            request.DeadlineAtUtc,
            bill.CourseCode,
            bill.CourseName,
            bill.BillNumber,
            bill.DeferralCount);

    internal static DeferExtensionRequestResponse ToResponse(DeferExtensionRequestProjection projection)
        => new(
            projection.RequestId,
            projection.BillId,
            projection.CourseEnrollmentId,
            projection.PersonId,
            projection.OrganizationId,
            projection.StatusCode,
            projection.RequestedAtUtc,
            projection.RequestedByLoginAccountId,
            projection.ReviewedAtUtc,
            projection.ReviewedByLoginAccountId,
            projection.DeadlineAtUtc,
            projection.CourseCode,
            projection.CourseName,
            projection.BillNumber,
            projection.DeferralCount);
}

internal sealed class ListDeferExtensionRequestsHandler(
    IDeferExtensionRequestRepository requests,
    IAdminAccessControl adminAccess)
    : IQueryHandler<ListDeferExtensionRequestsQuery, PageResponse<DeferExtensionRequestResponse>>
{
    public async Task<Result<PageResponse<DeferExtensionRequestResponse>>> Handle(
        ListDeferExtensionRequestsQuery query,
        CancellationToken cancellationToken)
    {
        Result<AdminOrganizationScope> scopeResult = ResolveSchoolAdminScope(
            adminAccess,
            query.Request.OrganizationId);
        if (scopeResult.IsFailure)
            return Result<PageResponse<DeferExtensionRequestResponse>>.Failure(scopeResult.Error);

        AdminOrganizationScope scope = scopeResult.Value;
        PageResponse<DeferExtensionRequestProjection> page = await requests.ListAsync(
            scope.OrganizationId,
            scope.ScopedOrganizationIds,
            scope.HasGlobalAccess,
            query.Request.StatusCode,
            query.Request.Page,
            query.Request.PageSize,
            cancellationToken);

        return Result<PageResponse<DeferExtensionRequestResponse>>.Success(
            new PageResponse<DeferExtensionRequestResponse>(
                page.Items.Select(CreateDeferExtensionRequestHandler.ToResponse).ToArray(),
                page.Page,
                page.PageSize,
                page.TotalCount));
    }

    internal static Result<AdminOrganizationScope> ResolveSchoolAdminScope(
        IAdminAccessControl adminAccess,
        long? requestedOrganizationId)
    {
        if (!adminAccess.IsSchoolAdmin)
            return Result<AdminOrganizationScope>.Failure(CourseBillingErrors.SchoolAdminRequired);

        AdminOrganizationScope scope = adminAccess.ResolveOrganizationFilter(requestedOrganizationId);
        if (!scope.HasAccess)
            return Result<AdminOrganizationScope>.Failure(CourseBillingErrors.OrganizationOutsideScope);

        if (scope.OrganizationId is null && scope.ScopedOrganizationIds.Count == 0)
            return Result<AdminOrganizationScope>.Failure(CourseBillingErrors.OrganizationRequired);

        return Result<AdminOrganizationScope>.Success(scope);
    }
}

internal sealed class ApproveDeferExtensionRequestHandler(
    IDeferExtensionRequestRepository requests,
    ICoursePaymentGateway billingPolicies,
    IAdminAccessControl adminAccess,
    ICurrentUser currentUser,
    IClock clock)
    : ICommandHandler<ApproveDeferExtensionRequestCommand, DeferExtensionRequestResponse>
{
    public async Task<Result<DeferExtensionRequestResponse>> Handle(
        ApproveDeferExtensionRequestCommand command,
        CancellationToken cancellationToken)
    {
        return await ReviewAsync(
            requests,
            billingPolicies,
            adminAccess,
            currentUser,
            clock,
            command.RequestId,
            approve: true,
            cancellationToken);
    }

    internal static async Task<Result<DeferExtensionRequestResponse>> ReviewAsync(
        IDeferExtensionRequestRepository requests,
        ICoursePaymentGateway billingPolicies,
        IAdminAccessControl adminAccess,
        ICurrentUser currentUser,
        IClock clock,
        long requestId,
        bool approve,
        CancellationToken cancellationToken)
    {
        if (!adminAccess.IsSchoolAdmin)
            return Result<DeferExtensionRequestResponse>.Failure(CourseBillingErrors.SchoolAdminRequired);
        if (currentUser.UserAccountId is not long actorId)
            return Result<DeferExtensionRequestResponse>.Failure(CourseBillingErrors.ActorRequired);

        DeferExtensionReviewAggregate? aggregate = await requests.FindForReviewAsync(
            requestId,
            cancellationToken);
        if (aggregate is null)
            return Result<DeferExtensionRequestResponse>.Failure(CourseBillingErrors.DeferExtensionRequestNotFound);

        Result scope = adminAccess.EnsureCanAccessOrganization(aggregate.Request.OrganizationId);
        if (scope.IsFailure)
            return Result<DeferExtensionRequestResponse>.Failure(scope.Error);

        DateTime now = clock.UtcNow.UtcDateTime;
        Result reviewResult;
        if (approve)
        {
            Result grantResult = aggregate.Bill.GrantDeferExtension(now);
            if (grantResult.IsFailure)
                return Result<DeferExtensionRequestResponse>.Failure(CourseBillingErrors.DeferExtensionNotAllowed);

            reviewResult = aggregate.Request.Approve(actorId, now);
        }
        else
        {
            BillingPolicySnapshot policy = await billingPolicies.GetBillingPolicyAsync(
                aggregate.Request.OrganizationId,
                cancellationToken);
            reviewResult = aggregate.Request.Reject(
                actorId,
                policy.RejectionGracePeriodDays,
                now);
        }

        if (reviewResult.IsFailure)
            return Result<DeferExtensionRequestResponse>.Failure(CourseBillingErrors.DeferExtensionNotAllowed);

        await requests.SaveChangesAsync(cancellationToken);
        return Result<DeferExtensionRequestResponse>.Success(new DeferExtensionRequestResponse(
            aggregate.Request.Id,
            aggregate.Request.BillId,
            aggregate.Request.CourseEnrollmentId,
            aggregate.Request.PersonId,
            aggregate.Request.OrganizationId,
            aggregate.Request.StatusCode,
            aggregate.Request.RequestedAtUtc,
            aggregate.Request.RequestedByLoginAccountId,
            aggregate.Request.ReviewedAtUtc,
            aggregate.Request.ReviewedByLoginAccountId,
            aggregate.Request.DeadlineAtUtc,
            null,
            null,
            null,
            null));
    }
}

internal sealed class RejectDeferExtensionRequestHandler(
    IDeferExtensionRequestRepository requests,
    ICoursePaymentGateway billingPolicies,
    IAdminAccessControl adminAccess,
    ICurrentUser currentUser,
    IClock clock)
    : ICommandHandler<RejectDeferExtensionRequestCommand, DeferExtensionRequestResponse>
{
    public async Task<Result<DeferExtensionRequestResponse>> Handle(
        RejectDeferExtensionRequestCommand command,
        CancellationToken cancellationToken)
        => await ApproveDeferExtensionRequestHandler.ReviewAsync(
            requests,
            billingPolicies,
            adminAccess,
            currentUser,
            clock,
            command.RequestId,
            approve: false,
            cancellationToken);
}
