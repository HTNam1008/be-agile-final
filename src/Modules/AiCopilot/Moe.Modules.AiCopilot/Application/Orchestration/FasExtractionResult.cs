namespace Moe.Modules.AiCopilot.Application.Orchestration;

public sealed record FasExtractionResult(string Status, object? Value = null, string? Message = null)
{
    public static FasExtractionResult Accepted(object? value = null) => new("ACCEPTED", value);
    public static FasExtractionResult Clarify(string message) => new("CLARIFY", Message: message);
    public static FasExtractionResult ManualFallback(string message) => new("MANUAL_FALLBACK", Message: message);
}
