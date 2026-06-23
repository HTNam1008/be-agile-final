using FluentValidation;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.FasPayment.Contracts.Payments;
using Moe.Modules.FasPayment.Domain.Payments;
using Moe.Modules.FasPayment.IGateway.Payments;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Application.PaymentPlans;

internal sealed record CreateCoursePaymentPlanCommand(
    long CourseId,
    CreateCoursePaymentPlanRequest Request) : ICommand<CoursePaymentPlanResponse>;

internal sealed record ListCoursePaymentPlansQuery(long CourseId)
    : IQuery<IReadOnlyCollection<CoursePaymentPlanResponse>>;

internal sealed class CreateCoursePaymentPlanRequestValidator : AbstractValidator<CreateCoursePaymentPlanRequest>
{
    public CreateCoursePaymentPlanRequestValidator()
    {
        RuleFor(request => request.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.PlanTypeCode)
            .Must(code => code is PaymentPlanTypeCodes.FullPayment or PaymentPlanTypeCodes.Installment);
        RuleFor(request => request.InstallmentCount)
            .Must((request, count) => request.PlanTypeCode == PaymentPlanTypeCodes.FullPayment
                ? count == 1
                : count is 3 or 6);
    }
}

internal sealed class CreateCoursePaymentPlanHandler(
    IPaymentCheckoutRepository payments,
    ICoursePaymentGateway courses,
    ICurrentUser currentUser,
    IAdminAccessControl adminAccess,
    IClock clock) : ICommandHandler<CreateCoursePaymentPlanCommand, CoursePaymentPlanResponse>
{
    public async Task<Result<CoursePaymentPlanResponse>> Handle(
        CreateCoursePaymentPlanCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !currentUser.Roles.Any(role => role is FasRoles.HqAdmin or FasRoles.SchoolAdmin))
            return Result<CoursePaymentPlanResponse>.Failure(PaymentApplicationErrors.AdministratorRequired);

        long? organizationId = await courses.FindCourseOrganizationIdAsync(command.CourseId, cancellationToken);
        if (organizationId is null)
            return Result<CoursePaymentPlanResponse>.Failure(PaymentApplicationErrors.CourseNotFound);
        if (!adminAccess.CanAccessOrganization(organizationId.Value))
            return Result<CoursePaymentPlanResponse>.Failure(PaymentApplicationErrors.CourseForbidden);

        int version = await payments.GetNextPlanVersionAsync(command.CourseId, cancellationToken);
        Result<CoursePaymentPlan> plan = CoursePaymentPlan.Create(
            command.CourseId,
            command.Request.DisplayName,
            command.Request.PlanTypeCode,
            command.Request.InstallmentCount,
            version,
            clock.UtcNow.UtcDateTime);
        if (plan.IsFailure) return Result<CoursePaymentPlanResponse>.Failure(plan.Error);

        await payments.AddPlanAsync(plan.Value, cancellationToken);
        return Result<CoursePaymentPlanResponse>.Success(ToResponse(plan.Value));
    }

    private static CoursePaymentPlanResponse ToResponse(CoursePaymentPlan plan) => new(
        plan.Id,
        plan.CourseId,
        plan.DisplayName,
        plan.PlanTypeCode,
        plan.CurrencyCode,
        plan.InstallmentCount,
        plan.Version,
        plan.IsActive);
}

internal sealed class ListCoursePaymentPlansHandler(IPaymentCheckoutRepository payments)
    : IQueryHandler<ListCoursePaymentPlansQuery, IReadOnlyCollection<CoursePaymentPlanResponse>>
{
    public async Task<Result<IReadOnlyCollection<CoursePaymentPlanResponse>>> Handle(
        ListCoursePaymentPlansQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<CoursePaymentPlan> plans = await payments.ListActivePlansAsync(query.CourseId, cancellationToken);
        CoursePaymentPlanResponse[] response = plans.Select(plan => new CoursePaymentPlanResponse(
            plan.Id,
            plan.CourseId,
            plan.DisplayName,
            plan.PlanTypeCode,
            plan.CurrencyCode,
            plan.InstallmentCount,
            plan.Version,
            plan.IsActive)).ToArray();
        return Result<IReadOnlyCollection<CoursePaymentPlanResponse>>.Success(response);
    }
}
