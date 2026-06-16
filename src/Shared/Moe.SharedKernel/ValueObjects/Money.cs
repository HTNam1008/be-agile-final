namespace Moe.SharedKernel.ValueObjects;

public readonly record struct Money
{
    public Money(decimal amount, string currencyCode)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        Amount = decimal.Round(amount, 4, MidpointRounding.ToEven);
        CurrencyCode = Normalize(currencyCode);
    }
    public decimal Amount { get; }
    public string CurrencyCode { get; }
    public static Money Zero(string currencyCode) => new(0m, currencyCode);
    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, CurrencyCode);
    }
    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        if (other.Amount > Amount) throw new InvalidOperationException("Amount cannot become negative.");
        return new Money(Amount - other.Amount, CurrencyCode);
    }
    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(CurrencyCode, other.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Currency mismatch.");
    }
    private static string Normalize(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length != 3)
            throw new ArgumentException("Currency code must contain three characters.", nameof(code));
        return code.Trim().ToUpperInvariant();
    }
}
