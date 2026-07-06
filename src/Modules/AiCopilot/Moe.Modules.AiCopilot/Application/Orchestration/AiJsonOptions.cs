using System.Text.Json;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

/// <summary>
/// Shared JsonSerializerOptions for the AI Copilot module.
/// Centralises the CA1869 fix: one static readonly instance instead of 13 per-class allocations.
/// </summary>
internal static class AiJsonOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web);
}
