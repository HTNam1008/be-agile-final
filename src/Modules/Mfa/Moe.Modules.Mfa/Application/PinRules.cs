namespace Moe.Modules.Mfa.Application;

internal static class PinRules
{
    public static bool IsValid(string? pin)
    {
        return pin is { Length: 4 } && pin.All(char.IsDigit);
    }
}
