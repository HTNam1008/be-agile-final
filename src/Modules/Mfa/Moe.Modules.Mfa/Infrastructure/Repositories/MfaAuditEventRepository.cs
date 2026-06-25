using Microsoft.AspNetCore.Http;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.Mfa.Domain;
using Moe.Modules.Mfa.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.Mfa.Infrastructure.Repositories;

internal sealed class MfaAuditEventRepository(
    MoeDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    IClock clock) : IMfaAuditEventRepository
{
    public void Add(
        long loginAccountId,
        Guid? challengeId,
        string eventCode,
        long? performedByAccountId = null,
        string? reason = null)
    {
        HttpContext? httpContext = httpContextAccessor.HttpContext;
        string? ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString();
        string? userAgent = httpContext?.Request.Headers.UserAgent.ToString();

        dbContext.Add(new LoginMfaAuditEvent(
            loginAccountId,
            challengeId,
            eventCode,
            performedByAccountId,
            reason,
            ipAddress,
            userAgent,
            clock.UtcNow.UtcDateTime));
    }
}
