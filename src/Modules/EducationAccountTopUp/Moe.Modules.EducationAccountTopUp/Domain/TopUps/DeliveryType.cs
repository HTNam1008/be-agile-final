namespace Moe.Modules.EducationAccountTopUp.Domain.TopUps;

public static class DeliveryType
{
    public const string Instant = "INSTANT";
    public const string FixedContract = "FIXED_CONTRACT";
    public const string ConditionalRecurring = "CONDITIONAL_RECURRING";

    public static readonly string[] ValidTypes = { Instant, FixedContract, ConditionalRecurring };

    public static bool IsValid(string? type) =>
        !string.IsNullOrWhiteSpace(type) && ValidTypes.Contains(type.ToUpperInvariant());
}
