using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.Contracts.BillingStatements;
using Moe.Modules.CourseBilling.IGateway.Fas;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.EducationAccountTopUp.IGateway.Accounts;
using Moe.Modules.FasPayment.Application;
using Moe.Modules.FasPayment.Application.Notifications;
using Moe.Modules.FasPayment.Contracts.Payments;
using Moe.Modules.FasPayment.Domain.Payments;
using Moe.Modules.FasPayment.IGateway.Payments;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Application.StatementPayments;

internal sealed record PreviewStatementPaymentQuery(
    long StatementId,
    IReadOnlyCollection<long>? BillIds = null) : IQuery<StatementPaymentPreviewResponse>;
internal sealed record PayBillingStatementCommand(long StatementId, PayBillingStatementRequest Request) : ICommand<PayBillingStatementResponse>;
internal sealed record CancelBillingStatementPaymentCommand(long StatementId, long PaymentId) : ICommand;
internal sealed record DeferBillingStatementCommand(long StatementId, DeferBillingStatementRequest Request) : ICommand<DeferBillingStatementResponse>;
internal sealed record GetPendingEnrollmentPaymentQuery(long CourseEnrollmentId) : IQuery<PendingEnrollmentPaymentResponse?>;

internal sealed class PayBillingStatementRequestValidator : AbstractValidator<PayBillingStatementRequest>
{
    public PayBillingStatementRequestValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(120);
        RuleFor(x => x.FundingOptionCode).Must(code =>
            code is PaymentFundingOptionCodes.EducationAccountOnly
                or PaymentFundingOptionCodes.OnlineOnly
                or PaymentFundingOptionCodes.EducationAccountThenOnline);
        RuleForEach(x => x.BillIds)
            .GreaterThan(0)
            .When(x => x.BillIds is not null);
    }
}

internal sealed class GetPendingEnrollmentPaymentHandler(
    IPaymentCheckoutRepository payments,
    IBillingStatementRepository billingStatements,
    StatementPaymentPreviewBuilder previewBuilder,
    ICurrentUser currentUser,
    IClock clock) : IQueryHandler<GetPendingEnrollmentPaymentQuery, PendingEnrollmentPaymentResponse?>
{
    public async Task<Result<PendingEnrollmentPaymentResponse?>> Handle(
        GetPendingEnrollmentPaymentQuery query,
        CancellationToken ct)
    {
        if (!currentUser.TryGetStudent(out long personId))
            return Result<PendingEnrollmentPaymentResponse?>.Failure(PaymentApplicationErrors.StudentRequired);

        Payment? payment = await payments.FindActiveStatementPaymentForEnrollmentAsync(
            query.CourseEnrollmentId,
            personId,
            ct);

        PendingEnrollmentBill? pendingBill = await payments.FindPendingEnrollmentBillAsync(
            query.CourseEnrollmentId,
            personId,
            ct);
        if (pendingBill is null)
        {
            return Result<PendingEnrollmentPaymentResponse?>.Success(null);
        }

        int year = pendingBill.CurrentDueDate.Year;
        int month = pendingBill.CurrentDueDate.Month;
        var statement = await billingStatements.GetOrCreateAsync(
            personId,
            year,
            month,
            clock.UtcNow.UtcDateTime,
            ct);
        IReadOnlyCollection<PendingEnrollmentFasReservation> reservations =
            await payments.ListPendingFasReservationsForEnrollmentAsync(query.CourseEnrollmentId, personId, ct);

        long[] billIds = payment is null
            ? [pendingBill.BillId]
            : (await payments.ListPaymentAllocationsAsync(payment.Id, ct))
                .Select(x => x.BillId)
                .Distinct()
                .ToArray();
        if (billIds.Length == 0)
        {
            billIds = [pendingBill.BillId];
        }
        BillingStatementItemResponse? billItem = statement.Items
            .FirstOrDefault(item => billIds.Contains(item.BillId));
        Result<StatementPaymentPreviewResponse> previewResult = await previewBuilder.BuildAsync(
            personId,
            payment?.BillingStatementId ?? statement.BillingStatementId,
            billIds,
            ct);
        if (previewResult.IsFailure)
            return Result<PendingEnrollmentPaymentResponse?>.Failure(previewResult.Error);

        StatementPaymentCheckoutSession? checkout = payment is null
            ? null
            : await payments.FindCheckoutByPaymentAsync(payment.Id, ct);
        return Result<PendingEnrollmentPaymentResponse?>.Success(new(
            query.CourseEnrollmentId,
            payment?.BillingStatementId ?? statement.BillingStatementId,
            year,
            month,
            payment?.Id,
            payment?.PaymentStatusCode,
            payment?.EducationAccountAmount ?? 0m,
            payment?.OnlinePaymentAmount ?? 0m,
            checkout?.CheckoutUrl,
            checkout?.Id,
            checkout?.ExpiresAtUtc,
            billIds,
            reservations.Select(x => new PendingEnrollmentFasSubsidyResponse(
                x.FasApplicationSchemeId,
                x.SchemeName,
                x.AppliedAmount,
                x.StatusCode)).ToArray(),
            billItem,
            previewResult.Value));
    }
}

internal sealed class PreviewStatementPaymentHandler(
    StatementPaymentPreviewBuilder previewBuilder,
    ICurrentUser currentUser)
    : IQueryHandler<PreviewStatementPaymentQuery, StatementPaymentPreviewResponse>
{
    public async Task<Result<StatementPaymentPreviewResponse>> Handle(PreviewStatementPaymentQuery query, CancellationToken ct)
    {
        if (!currentUser.TryGetStudent(out long personId))
            return Result<StatementPaymentPreviewResponse>.Failure(PaymentApplicationErrors.StudentRequired);

        return await previewBuilder.BuildAsync(personId, query.StatementId, query.BillIds, ct);
    }
}

internal sealed class StatementPaymentPreviewBuilder(
    ICoursePaymentGateway billing,
    IEducationAccountPaymentGateway accounts)
{
    public async Task<Result<StatementPaymentPreviewResponse>> BuildAsync(
        long personId,
        long statementId,
        IReadOnlyCollection<long>? billIds,
        CancellationToken ct)
    {
        PayableStatement? statement = await billing.FindPayableStatementAsync(statementId, personId, ct);
        if (statement is null) return Result<StatementPaymentPreviewResponse>.Failure(PaymentApplicationErrors.BillNotFound);
        Result<IReadOnlyCollection<PayableStatementBill>> selectedBillsResult = StatementBillSelection.Select(
            statement,
            billIds);
        if (selectedBillsResult.IsFailure)
            return Result<StatementPaymentPreviewResponse>.Failure(selectedBillsResult.Error);

        decimal selectedOutstandingAmount = selectedBillsResult.Value.Sum(bill => bill.OutstandingAmount);
        EducationAccountPaymentBalance? balance = await accounts.GetAvailableBalanceAsync(personId, ct);
        decimal available = balance?.AvailableBalance ?? 0m;
        decimal education = Math.Min(available, selectedOutstandingAmount);
        decimal online = selectedOutstandingAmount - education;
        string recommended = available >= selectedOutstandingAmount
            ? PaymentFundingOptionCodes.EducationAccountOnly
            : available > 0m
                ? PaymentFundingOptionCodes.EducationAccountThenOnline
                : PaymentFundingOptionCodes.OnlineOnly;
        StatementFundingOptionResponse[] options =
        [
            new(
                PaymentFundingOptionCodes.EducationAccountOnly,
                "Education Account",
                available >= selectedOutstandingAmount,
                selectedOutstandingAmount,
                0m,
                available >= selectedOutstandingAmount
                    ? null
                    : "Education Account balance is not enough for the selected bills."),
            new(
                PaymentFundingOptionCodes.OnlineOnly,
                "Online payment",
                true,
                0m,
                selectedOutstandingAmount,
                null),
            new(
                PaymentFundingOptionCodes.EducationAccountThenOnline,
                "Education Account + online",
                available > 0m && available < selectedOutstandingAmount,
                education,
                online,
                available <= 0m
                    ? "No Education Account balance is available."
                    : available >= selectedOutstandingAmount
                        ? "Education Account can cover the selected bills."
                        : null)
        ];
        return Result<StatementPaymentPreviewResponse>.Success(new(
            statement.BillingStatementId, selectedOutstandingAmount,
            balance?.CurrentBalance ?? 0m, balance?.HeldBalance ?? 0m, available, education,
            online, statement.CurrencyCode, recommended, options));
    }
}

internal sealed class PayBillingStatementHandler(
    IPaymentCheckoutRepository payments,
    ICoursePaymentGateway billing,
    IFasCourseSubsidyGateway fasSubsidies,
    IEducationAccountPaymentGateway accounts,
    IStripePaymentGateway stripe,
    ICurrentUser currentUser,
    IClock clock,
    PaymentNotificationEmailService paymentNotifications) : ICommandHandler<PayBillingStatementCommand, PayBillingStatementResponse>
{
    public async Task<Result<PayBillingStatementResponse>> Handle(PayBillingStatementCommand command, CancellationToken ct)
    {
        if (!currentUser.TryGetStudent(out long personId))
            return Result<PayBillingStatementResponse>.Failure(PaymentApplicationErrors.StudentRequired);

        PayableStatement? statement = await billing.FindPayableStatementAsync(command.StatementId, personId, ct);
        if (statement is null) return Result<PayBillingStatementResponse>.Failure(PaymentApplicationErrors.BillNotFound);
        Result<IReadOnlyCollection<PayableStatementBill>> selectedBillsResult = StatementBillSelection.Select(
            statement,
            command.Request.BillIds);
        if (selectedBillsResult.IsFailure)
            return Result<PayBillingStatementResponse>.Failure(selectedBillsResult.Error);
        IReadOnlyCollection<PayableStatementBill> selectedBills = selectedBillsResult.Value;
        decimal selectedOutstandingAmount = selectedBills.Sum(bill => bill.OutstandingAmount);

        Result<PayBillingStatementResponse?> existingAttempt =
            await ResumeOrCloseExistingAttemptAsync(statement.BillingStatementId, personId, ct);
        if (!existingAttempt.IsSuccess)
            return Result<PayBillingStatementResponse>.Failure(existingAttempt.Error);
        if (existingAttempt.Value is not null)
            return Result<PayBillingStatementResponse>.Success(existingAttempt.Value);

        EducationAccountPaymentBalance? balance = await accounts.GetAvailableBalanceAsync(personId, ct);
        Result<StatementFundingAllocation> allocationResult = ResolveFundingAllocation(
            command.Request.FundingOptionCode,
            selectedOutstandingAmount,
            balance?.AvailableBalance ?? 0m);
        if (allocationResult.IsFailure)
            return Result<PayBillingStatementResponse>.Failure(allocationResult.Error);

        decimal educationAmount = allocationResult.Value.EducationAccountAmount;
        decimal onlineAmount = allocationResult.Value.OnlinePaymentAmount;
        DateTime now = clock.UtcNow.UtcDateTime;
        Payment payment = Payment.StartStatementPayment(statement.BillingStatementId, personId,
            selectedOutstandingAmount, educationAmount, onlineAmount, command.Request.IdempotencyKey, now);
        List<PaymentPart> parts = [];
        PaymentPart? educationPart = null;
        if (educationAmount > 0m)
        {
            educationPart = PaymentPart.Create(0, 1, PaymentMethodCodes.EducationAccount, educationAmount,
                onlineAmount > 0m ? PaymentPartStatusCodes.Reserved : PaymentPartStatusCodes.Pending, now);
            educationPart.AssignEducationAccount(balance!.EducationAccountId, null);
            parts.Add(educationPart);
        }
        if (onlineAmount > 0m)
            parts.Add(PaymentPart.Create(0, parts.Count + 1, PaymentMethodCodes.OnlinePayment, onlineAmount, PaymentPartStatusCodes.Pending, now));
        PaymentAllocation[] allocations = selectedBills.Select(x =>
            new PaymentAllocation(0, x.BillId, x.BillingStatementItemId, x.OutstandingAmount, now)).ToArray();
        await payments.AddStatementPaymentAsync(payment, parts, allocations, ct);

        if (onlineAmount == 0m)
        {
            long transactionId = await accounts.DebitImmediatelyAsync(
                personId, educationPart!.Id, educationAmount, currentUser.UserAccountId, ct);
            educationPart.MarkCompleted(PaymentPartStatusCodes.Captured, now, transactionId);
            foreach (PaymentAllocation allocation in allocations) allocation.MarkApplied();
            await billing.ApplyStatementPaymentAsync(statement.BillingStatementId,
                allocations.Select(x => new BillPaymentAllocation(x.BillId, x.AllocatedAmount)).ToArray(), now, ct);
            await fasSubsidies.RedeemPendingRedemptionsForBillsAsync(
                allocations.Select(x => x.BillId).ToArray(),
                now,
                ct);
            payment.MarkSuccessful(now);
            await paymentNotifications.SendPaymentSucceededAsync(payment, now, ct);
            await payments.ExecuteInTransactionAsync(_ => Task.CompletedTask, ct);
            return Result<PayBillingStatementResponse>.Success(new(
                payment.Id, payment.PaymentStatusCode, educationAmount, 0m, null, null, null, false));
        }

        if (educationPart is not null)
        {
            long holdId = await accounts.ReserveAsync(
                personId,
                educationPart.Id,
                educationAmount,
                now.Add(PaymentCheckoutPolicy.Lifetime),
                ct);
            educationPart.AssignAccountHold(holdId);
        }
        StatementPaymentCheckoutSession checkout = StatementPaymentCheckoutSession.Create(
            payment.Id, statement.BillingStatementId, personId, onlineAmount, now);
        await payments.AddCheckoutAsync(checkout, ct);
        try
        {
            StripeCheckoutGatewayResult provider = await stripe.CreateCheckoutAsync(
                new StripeCheckoutGatewayRequest(checkout.IdempotencyKey, checkout.Id, 0, 0,
                    $"Monthly billing statement {statement.BillingStatementId}", statement.CurrencyCode,
                    decimal.ToInt64(onlineAmount * 100m), 1, null,
                    now.Add(PaymentCheckoutPolicy.Lifetime)), ct);
            checkout.AssignProviderCheckout(
                provider.ProviderSessionId,
                provider.ProviderPriceId,
                provider.CheckoutUrl,
                provider.ExpiresAtUtc,
                now);
            parts.Single(x => x.PaymentMethodCode == PaymentMethodCodes.OnlinePayment).AssignProvider("STRIPE", provider.ProviderSessionId);
            await payments.ExecuteInTransactionAsync(_ => Task.CompletedTask, ct);
            return Result<PayBillingStatementResponse>.Success(new(
                payment.Id,
                payment.PaymentStatusCode,
                educationAmount,
                onlineAmount,
                provider.CheckoutUrl,
                checkout.Id,
                provider.ExpiresAtUtc,
                false));
        }
        catch (PaymentProviderUnavailableException)
        {
            if (educationPart?.AccountHoldId is long holdId) await accounts.ReleaseAsync(holdId, ct);
            payment.MarkFailed(now);
            await paymentNotifications.SendStatementPaymentFailedAsync(
                payment,
                "The payment gateway was unavailable. Please try again.",
                ct);
            await payments.ExecuteInTransactionAsync(_ => Task.CompletedTask, ct);
            return Result<PayBillingStatementResponse>.Failure(PaymentDomainErrors.ProviderUnavailable);
        }
    }

    private async Task<Result<PayBillingStatementResponse?>> ResumeOrCloseExistingAttemptAsync(
        long billingStatementId,
        long personId,
        CancellationToken cancellationToken)
    {
        Payment? activePayment = await payments.FindActiveStatementPaymentAsync(
            billingStatementId,
            personId,
            cancellationToken);
        if (activePayment is null)
            return Result<PayBillingStatementResponse?>.Success(null);

        DateTime now = clock.UtcNow.UtcDateTime;
        StatementPaymentCheckoutSession? checkout = await payments.FindCheckoutByPaymentAsync(
            activePayment.Id,
            cancellationToken);
        if (checkout?.CanResume(now) == true)
        {
            return Result<PayBillingStatementResponse?>.Success(new(
                activePayment.Id,
                activePayment.PaymentStatusCode,
                activePayment.EducationAccountAmount,
                activePayment.OnlinePaymentAmount,
                checkout.CheckoutUrl,
                checkout.Id,
                checkout.ExpiresAtUtc,
                true));
        }

        DateTime expiresAtUtc = checkout?.ExpiresAtUtc
            ?? activePayment.InitiatedAtUtc.Add(PaymentCheckoutPolicy.Lifetime);
        if (expiresAtUtc > now)
            return Result<PayBillingStatementResponse?>.Failure(PaymentDomainErrors.StatementPaymentInProgress);

        if (!string.IsNullOrWhiteSpace(checkout?.ProviderCheckoutSessionId))
        {
            try
            {
                await stripe.ExpireCheckoutAsync(checkout.ProviderCheckoutSessionId, cancellationToken);
            }
            catch (PaymentProviderUnavailableException)
            {
                return Result<PayBillingStatementResponse?>.Failure(PaymentDomainErrors.ProviderUnavailable);
            }
        }

        IReadOnlyCollection<PaymentPart> parts = await payments.ListPaymentPartsAsync(
            activePayment.Id,
            cancellationToken);
        PaymentPart? educationPart = parts.SingleOrDefault(
            part => part.PaymentMethodCode == PaymentMethodCodes.EducationAccount);
        if (educationPart?.AccountHoldId is long holdId)
        {
            await accounts.ReleaseAsync(holdId, cancellationToken);
            educationPart.MarkCompleted(PaymentPartStatusCodes.Released, now);
        }
        PaymentPart? onlinePart = parts.SingleOrDefault(
            part => part.PaymentMethodCode == PaymentMethodCodes.OnlinePayment);
        onlinePart?.MarkCompleted(PaymentPartStatusCodes.Failed, now);
        checkout?.ExpireBeforePayment(now);
        activePayment.MarkExpired(now);
        await paymentNotifications.SendPaymentExpiredAsync(activePayment, cancellationToken);
        await payments.ExecuteInTransactionAsync(_ => Task.CompletedTask, cancellationToken);
        return Result<PayBillingStatementResponse?>.Success(null);
    }

    private static Result<StatementFundingAllocation> ResolveFundingAllocation(
        string fundingOptionCode,
        decimal statementOutstandingAmount,
        decimal educationAccountAvailableBalance)
    {
        return fundingOptionCode switch
        {
            PaymentFundingOptionCodes.EducationAccountOnly
                when educationAccountAvailableBalance >= statementOutstandingAmount
                => Result<StatementFundingAllocation>.Success(
                    new(statementOutstandingAmount, 0m)),
            PaymentFundingOptionCodes.EducationAccountOnly
                => Result<StatementFundingAllocation>.Failure(PaymentDomainErrors.InsufficientBalance),
            PaymentFundingOptionCodes.OnlineOnly
                => Result<StatementFundingAllocation>.Success(
                    new(0m, statementOutstandingAmount)),
            PaymentFundingOptionCodes.EducationAccountThenOnline
                when educationAccountAvailableBalance > 0m
                    && educationAccountAvailableBalance < statementOutstandingAmount
                => Result<StatementFundingAllocation>.Success(
                    new(
                        educationAccountAvailableBalance,
                        statementOutstandingAmount - educationAccountAvailableBalance)),
            PaymentFundingOptionCodes.EducationAccountThenOnline
                when educationAccountAvailableBalance >= statementOutstandingAmount
                => Result<StatementFundingAllocation>.Success(
                    new(statementOutstandingAmount, 0m)),
            PaymentFundingOptionCodes.EducationAccountThenOnline
                => Result<StatementFundingAllocation>.Success(
                    new(0m, statementOutstandingAmount)),
            _ => Result<StatementFundingAllocation>.Failure(PaymentDomainErrors.InvalidPaymentMethod)
        };
    }

    private sealed record StatementFundingAllocation(
        decimal EducationAccountAmount,
        decimal OnlinePaymentAmount);
}

internal sealed class CancelBillingStatementPaymentHandler(
    IPaymentCheckoutRepository payments,
    IEducationAccountPaymentGateway accounts,
    IStripePaymentGateway stripe,
    ICurrentUser currentUser,
    IClock clock,
    PaymentNotificationEmailService paymentNotifications) : ICommandHandler<CancelBillingStatementPaymentCommand>
{
    public async Task<Result> Handle(CancelBillingStatementPaymentCommand command, CancellationToken ct)
    {
        if (!currentUser.TryGetStudent(out long personId))
            return Result.Failure(PaymentApplicationErrors.StudentRequired);

        Payment? payment = await payments.FindPaymentAsync(command.PaymentId, ct);
        if (payment is null ||
            payment.BillingStatementId != command.StatementId ||
            payment.PayerPersonId != personId)
            return Result.Failure(PaymentApplicationErrors.BillNotFound);

        if (payment.PaymentStatusCode is not (PaymentStatusCodes.Initiated or PaymentStatusCodes.PendingOnlinePayment))
            return Result.Success();

        StatementPaymentCheckoutSession? checkout = await payments.FindCheckoutByPaymentAsync(payment.Id, ct);
        if (!string.IsNullOrWhiteSpace(checkout?.ProviderCheckoutSessionId))
        {
            try
            {
                await stripe.ExpireCheckoutAsync(checkout.ProviderCheckoutSessionId, ct);
            }
            catch (PaymentProviderUnavailableException)
            {
                // Cancelling checkout is a local recovery action for the student. Stripe can reject
                // expiry when the hosted session is already completed/expired/cancelled remotely, or
                // can be temporarily unavailable. In those cases we still release local holds and mark
                // the pending payment as cancelled so the student can retry instead of being stuck.
            }
        }

        DateTime now = clock.UtcNow.UtcDateTime;
        IReadOnlyCollection<PaymentPart> parts = await payments.ListPaymentPartsAsync(payment.Id, ct);
        PaymentPart? educationPart = parts.SingleOrDefault(
            part => part.PaymentMethodCode == PaymentMethodCodes.EducationAccount);
        if (educationPart?.AccountHoldId is long holdId)
        {
            await accounts.ReleaseAsync(holdId, ct);
            educationPart.MarkCompleted(PaymentPartStatusCodes.Released, now);
        }
        bool releasedEducationAccountHold = educationPart?.AccountHoldId is long;
        parts.SingleOrDefault(part => part.PaymentMethodCode == PaymentMethodCodes.OnlinePayment)
            ?.MarkCompleted(PaymentPartStatusCodes.Failed, now);
        checkout?.CancelBeforePayment(now);
        payment.MarkCancelled(now);
        try
        {
            await payments.ExecuteInTransactionAsync(_ => Task.CompletedTask, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Success();
        }
        await paymentNotifications.SendPaymentCancelledAsync(payment, now, releasedEducationAccountHold, ct);
        return Result.Success();
    }
}

internal static class StatementBillSelection
{
    public static Result<IReadOnlyCollection<PayableStatementBill>> Select(
        PayableStatement statement,
        IReadOnlyCollection<long>? billIds)
    {
        long[] requestedBillIds = billIds?
            .Where(id => id > 0)
            .Distinct()
            .ToArray() ?? [];

        if (requestedBillIds.Length == 0)
        {
            return Result<IReadOnlyCollection<PayableStatementBill>>.Success(statement.Bills);
        }

        PayableStatementBill[] selectedBills = statement.Bills
            .Where(bill => requestedBillIds.Contains(bill.BillId))
            .ToArray();

        if (selectedBills.Length != requestedBillIds.Length || selectedBills.Length == 0)
        {
            return Result<IReadOnlyCollection<PayableStatementBill>>.Failure(PaymentApplicationErrors.BillNotFound);
        }

        return Result<IReadOnlyCollection<PayableStatementBill>>.Success(selectedBills);
    }
}

internal sealed class DeferBillingStatementHandler(
    ICoursePaymentGateway billing,
    IEducationAccountPaymentGateway accounts,
    ICurrentUser currentUser,
    IClock clock) : ICommandHandler<DeferBillingStatementCommand, DeferBillingStatementResponse>
{
    public async Task<Result<DeferBillingStatementResponse>> Handle(DeferBillingStatementCommand command, CancellationToken ct)
    {
        if (!currentUser.TryGetStudent(out long personId) || currentUser.UserAccountId is not long actorId)
            return Result<DeferBillingStatementResponse>.Failure(PaymentApplicationErrors.StudentRequired);

        PayableStatement? statement = await billing.FindPayableStatementAsync(command.StatementId, personId, ct);
        if (statement is null)
            return Result<DeferBillingStatementResponse>.Failure(PaymentApplicationErrors.BillNotFound);

        long[] requestedBillIds = command.Request.BillIds?
            .Where(id => id > 0)
            .Distinct()
            .ToArray() ?? [];
        if (requestedBillIds.Length == 0)
            return Result<DeferBillingStatementResponse>.Failure(PaymentDomainErrors.NoDeferrableBills);

        PayableStatementBill[] selectedBills = statement.Bills
            .Where(bill => requestedBillIds.Contains(bill.BillId))
            .ToArray();

        if (selectedBills.Length != requestedBillIds.Length)
            return Result<DeferBillingStatementResponse>.Failure(PaymentDomainErrors.InvalidDeferral);
        if (selectedBills.Any(bill => !bill.IsInstallment))
            return Result<DeferBillingStatementResponse>.Failure(PaymentDomainErrors.FullPaymentCannotBeDeferred);
        if (selectedBills.Length == 0)
            return Result<DeferBillingStatementResponse>.Failure(PaymentDomainErrors.NoDeferrableBills);

        EducationAccountPaymentBalance? balance = await accounts.GetAvailableBalanceAsync(personId, ct);
        decimal availableBalance = balance?.AvailableBalance ?? 0m;
        DeferCoverableBillResponse[] coverableBills = selectedBills
            .Where(bill => availableBalance >= bill.OutstandingAmount)
            .OrderBy(bill => bill.OutstandingAmount)
            .ThenBy(bill => bill.CurrentDueDate)
            .ThenBy(bill => bill.BillId)
            .Select(bill => new DeferCoverableBillResponse(
                bill.BillId,
                bill.BillingStatementItemId,
                bill.OutstandingAmount,
                bill.CurrentDueDate,
                bill.CourseCode,
                bill.CourseName))
            .ToArray();
        if (coverableBills.Length > 0)
        {
            return Result<DeferBillingStatementResponse>.Success(new DeferBillingStatementResponse(
                Deferred: false,
                BlockedReasonCode: PaymentDomainErrors.EducationAccountCanCoverDeferral.Code,
                AvailableBalance: availableBalance,
                CoverableBillIds: coverableBills.Select(bill => bill.BillId).ToArray(),
                CoverableBills: coverableBills));
        }

        Result deferResult = await billing.DeferStatementAsync(
            command.StatementId,
            personId,
            selectedBills.Select(bill => bill.BillId).ToArray(),
            actorId,
            clock.UtcNow.UtcDateTime,
            ct);
        if (deferResult.IsFailure)
        {
            return Result<DeferBillingStatementResponse>.Failure(PaymentDomainErrors.InvalidDeferral);
        }

        return Result<DeferBillingStatementResponse>.Success(new DeferBillingStatementResponse(Deferred: true));
    }
}
