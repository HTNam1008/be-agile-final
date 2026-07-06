namespace Moe.Modules.AiCopilot.Application.Orchestration;

public sealed class ConcurrencyConflictException : InvalidOperationException
{
    public ConcurrencyConflictException(string message, Exception inner) : base(message, inner) { }
}
