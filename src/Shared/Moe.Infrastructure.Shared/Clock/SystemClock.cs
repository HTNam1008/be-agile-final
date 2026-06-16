using Moe.Application.Abstractions.Clock;
namespace Moe.Infrastructure.Shared.Clock;
public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
