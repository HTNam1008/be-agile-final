using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.FasPayment.Contracts.Payments;
using Moe.Modules.FasPayment.Domain.Payments;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.FasPayment.Application.StatementPayments;

public sealed record GetPaymentReceiptQuery(long PaymentId) : IQuery<PaymentReceiptResponse>;

internal sealed class GetPaymentReceiptHandler(
    PaymentReceiptService receipts,
    ICurrentUser currentUser)
    : IQueryHandler<GetPaymentReceiptQuery, PaymentReceiptResponse>
{
    public async Task<Result<PaymentReceiptResponse>> Handle(
        GetPaymentReceiptQuery query,
        CancellationToken cancellationToken)
    {
        if (!currentUser.TryGetStudent(out long personId))
            return Result<PaymentReceiptResponse>.Failure(PaymentApplicationErrors.StudentRequired);

        PaymentReceiptResponse? receipt = await receipts.BuildForPaymentAsync(
            query.PaymentId,
            personId,
            cancellationToken);

        return receipt is null
            ? Result<PaymentReceiptResponse>.Failure(PaymentApplicationErrors.ReceiptNotFound)
            : Result<PaymentReceiptResponse>.Success(receipt);
    }
}

internal sealed class PaymentReceiptService(MoeDbContext dbContext)
{
    public async Task<PaymentReceiptResponse?> BuildForPaymentAsync(
        long paymentId,
        long? personId,
        CancellationToken cancellationToken)
    {
        if (paymentId <= 0) return null;

        Payment? payment = await dbContext.Set<Payment>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate =>
                candidate.Id == paymentId &&
                (personId == null || candidate.PayerPersonId == personId.Value),
                cancellationToken);

        return payment is null
            ? null
            : await BuildForPaymentAsync(payment, cancellationToken);
    }

    public async Task<PaymentReceiptResponse?> BuildForPaymentAsync(
        Payment payment,
        CancellationToken cancellationToken)
    {
        if (!IsReceiptEligible(payment)) return null;

        Person? person = await dbContext.Set<Person>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == payment.PayerPersonId, cancellationToken);

        IReadOnlyCollection<PaymentReceiptPartResponse> parts =
            payment.Id > 0
                ? await BuildPartsAsync(payment.Id, cancellationToken)
                : [];

        IReadOnlyCollection<PaymentReceiptItemResponse> items =
            payment.Id > 0
                ? await BuildAllocatedItemsAsync(payment.Id, cancellationToken)
                : [];

        if (items.Count == 0 && payment.BillId > 0)
        {
            PaymentReceiptItemResponse? item = await BuildDirectBillItemAsync(payment.BillId, payment.PaymentAmount, cancellationToken);
            if (item is not null) items = [item];
        }

        string studentName = string.IsNullOrWhiteSpace(person?.OfficialFullName)
            ? "Student"
            : person.OfficialFullName.Trim();
        decimal totalPaid = payment.SuccessfulAmount > 0m ? payment.SuccessfulAmount : payment.PaymentAmount;

        return new PaymentReceiptResponse(
            payment.Id,
            payment.PaymentNumber,
            ReceiptNumber(payment),
            payment.CompletedAtUtc ?? payment.InitiatedAtUtc,
            payment.PaymentStatusCode,
            studentName,
            payment.BillingStatementId,
            "SGD",
            totalPaid,
            payment.EducationAccountAmount,
            payment.OnlinePaymentAmount > 0m ? payment.OnlinePaymentAmount : OnlineAmountFallback(payment),
            items,
            parts,
            payment.ProviderHostedInvoiceUrl,
            payment.ProviderInvoicePdfUrl,
            payment.ProviderReceiptUrl);
    }

    public static string ReceiptNumber(Payment payment)
        => payment.Id > 0
            ? $"MOE-RCPT-{payment.Id:D8}"
            : payment.ReceiptNumber ?? payment.PaymentNumber;

    private static bool IsReceiptEligible(Payment payment)
        => payment.PaymentStatusCode is PaymentStatusCodes.Successful
            or PaymentStatusCodes.PartiallyRefunded
            or PaymentStatusCodes.Refunded;

    private static decimal OnlineAmountFallback(Payment payment)
        => payment.EducationAccountAmount <= 0m ? payment.PaymentAmount : 0m;

    private async Task<IReadOnlyCollection<PaymentReceiptPartResponse>> BuildPartsAsync(
        long paymentId,
        CancellationToken cancellationToken)
        => await dbContext.Set<PaymentPart>()
            .AsNoTracking()
            .Where(part => part.PaymentId == paymentId)
            .OrderBy(part => part.SequenceNumber)
            .Select(part => new PaymentReceiptPartResponse(
                part.Id,
                part.PaymentMethodCode,
                part.PartAmount,
                part.PartStatusCode,
                part.EducationAccountId,
                part.AccountTransactionId,
                part.ProviderCode,
                part.ProviderReference))
            .ToArrayAsync(cancellationToken);

    private async Task<IReadOnlyCollection<PaymentReceiptItemResponse>> BuildAllocatedItemsAsync(
        long paymentId,
        CancellationToken cancellationToken)
        => await (
                from allocation in dbContext.Set<PaymentAllocation>().AsNoTracking()
                join bill in dbContext.Set<Bill>().AsNoTracking() on allocation.BillId equals bill.Id
                join enrollment in dbContext.Set<CourseEnrollment>().AsNoTracking() on bill.CourseEnrollmentId equals enrollment.Id
                join course in dbContext.Set<Course>().AsNoTracking() on enrollment.CourseId equals course.Id
                where allocation.PaymentId == paymentId
                orderby bill.CurrentDueDate, bill.SequenceNumber, bill.Id
                select new PaymentReceiptItemResponse(
                    bill.Id,
                    bill.BillNumber,
                    course.CourseCode,
                    course.CourseName,
                    bill.SequenceNumber,
                    bill.CurrentDueDate,
                    allocation.AllocatedAmount,
                    bill.OutstandingAmount))
            .ToArrayAsync(cancellationToken);

    private async Task<PaymentReceiptItemResponse?> BuildDirectBillItemAsync(
        long billId,
        decimal paidAmount,
        CancellationToken cancellationToken)
        => await (
                from bill in dbContext.Set<Bill>().AsNoTracking()
                join enrollment in dbContext.Set<CourseEnrollment>().AsNoTracking() on bill.CourseEnrollmentId equals enrollment.Id
                join course in dbContext.Set<Course>().AsNoTracking() on enrollment.CourseId equals course.Id
                where bill.Id == billId
                select new PaymentReceiptItemResponse(
                    bill.Id,
                    bill.BillNumber,
                    course.CourseCode,
                    course.CourseName,
                    bill.SequenceNumber,
                    bill.CurrentDueDate,
                    paidAmount,
                    bill.OutstandingAmount))
            .SingleOrDefaultAsync(cancellationToken);
}
