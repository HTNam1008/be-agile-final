using FluentValidation;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.FasPayment.Application;
using Moe.Modules.FasPayment.Contracts.Payments;
using Moe.Modules.FasPayment.Domain.Payments;
using Moe.Modules.FasPayment.IGateway.Payments;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Application.LegacyPayments;

public sealed record GetOutstandingBillsQuery : IQuery<OutstandingBillsResponse>;
internal sealed record PayOutstandingBillCommand(PayBillRequest Request) : ICommand<PayBillResponse>;

internal sealed class PayBillRequestValidator : AbstractValidator<PayBillRequest>
{
    public PayBillRequestValidator()
    {
        RuleFor(request => request.BillId).GreaterThan(0);
        RuleFor(request => request.PaymentMethodCode)
            .Must(code => code is PaymentMethodCodes.EducationAccount or PaymentMethodCodes.OnlineTender or PaymentMethodCodes.Card);
        RuleFor(request => request.IdempotencyKey).MaximumLength(120);
    }
}

internal sealed class GetOutstandingBillsHandler(
    ILegacyCoursePaymentGateway payments,
    ICurrentUser currentUser) : IQueryHandler<GetOutstandingBillsQuery, OutstandingBillsResponse>
{
    public async Task<Result<OutstandingBillsResponse>> Handle(
        GetOutstandingBillsQuery query,
        CancellationToken cancellationToken)
    {
        if (!currentUser.TryGetStudent(out long personId))
            return Result<OutstandingBillsResponse>.Failure(PaymentApplicationErrors.StudentRequired);
        return Result<OutstandingBillsResponse>.Success(
            await payments.ReadOutstandingBillsAsync(personId, cancellationToken));
    }
}

internal sealed class PayOutstandingBillHandler(
    ILegacyCoursePaymentGateway payments,
    ICurrentUser currentUser) : ICommandHandler<PayOutstandingBillCommand, PayBillResponse>
{
    public async Task<Result<PayBillResponse>> Handle(
        PayOutstandingBillCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentUser.TryGetStudent(out long personId))
            return Result<PayBillResponse>.Failure(PaymentApplicationErrors.StudentRequired);
        return await payments.PayBillAsync(
            personId,
            currentUser.UserAccountId,
            command.Request,
            cancellationToken);
    }
}
