using Moe.SharedKernel.Domain;

namespace Moe.Modules.CourseBilling.Domain.Billing;

internal sealed class BillingStatement : Entity<long>
{
    private BillingStatement() : base(0) { }

    private BillingStatement(long personId, int year, int month, DateTime createdAtUtc) : base(0)
    {
        PersonId = personId;
        StatementYear = year;
        StatementMonth = month;
        CurrencyCode = "SGD";
        StatementStatusCode = BillingStatementStatusCodes.Open;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public long PersonId { get; private set; }
    public int StatementYear { get; private set; }
    public int StatementMonth { get; private set; }
    public string CurrencyCode { get; private set; } = "SGD";
    public decimal TotalAmount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal OutstandingAmount { get; private set; }
    public string StatementStatusCode { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static BillingStatement Create(long personId, int year, int month, DateTime createdAtUtc)
    {
        if (personId <= 0 || year < 2000 || month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(personId));
        return new(personId, year, month, createdAtUtc);
    }

    public void Refresh(decimal total, decimal paid, DateTime updatedAtUtc)
    {
        TotalAmount = Money(total);
        PaidAmount = Money(paid);
        OutstandingAmount = Money(TotalAmount - PaidAmount);
        StatementStatusCode = OutstandingAmount == 0m
            ? BillingStatementStatusCodes.Paid
            : PaidAmount > 0m
                ? BillingStatementStatusCodes.PartiallyPaid
                : BillingStatementStatusCodes.Open;
        UpdatedAtUtc = updatedAtUtc;
    }

    private static decimal Money(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}

internal static class BillingStatementStatusCodes
{
    public const string Open = "OPEN";
    public const string PartiallyPaid = "PARTIALLY_PAID";
    public const string Paid = "PAID";
}
