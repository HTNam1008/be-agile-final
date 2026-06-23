namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

internal static class TopUpReadValidationRules
{
    internal const int MaxPageSize = 100;
    internal const int MaxPageNumber = 1_000_000;

    internal static bool HasValidDateRange(DateTime? from, DateTime? to)
        => !from.HasValue || !to.HasValue || from.Value < to.Value;

    internal static bool BeValidEnumValue<TEnum>(string? value)
        where TEnum : struct, Enum
        => string.IsNullOrWhiteSpace(value)
            || Enum.TryParse(value.Trim(), ignoreCase: true, out TEnum _);
}
