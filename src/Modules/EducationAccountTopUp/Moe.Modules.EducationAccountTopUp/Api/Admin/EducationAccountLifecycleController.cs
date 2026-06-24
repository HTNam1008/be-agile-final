using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Clock;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.EducationAccountTopUp.Application.Lifecycle;

namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/education-account-lifecycle")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class EducationAccountLifecycleController(
    EducationAccountLifecycleWorker lifecycleWorker,
    IClock clock) : ControllerBase
{
    [HttpPost("run-now")]
    [ProducesResponseType(typeof(ApiResponse<EducationAccountLifecycleRunNowResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RunNow(CancellationToken cancellationToken)
    {
        DateTimeOffset runAtUtc = clock.UtcNow;
        DateOnly today = DateOnly.FromDateTime(runAtUtc.UtcDateTime);
        EducationAccountLifecycleRunResult result = await lifecycleWorker.ProcessAsync(
            today,
            runAtUtc,
            cancellationToken);

        return ApiResponseFactory.Ok(
            new EducationAccountLifecycleRunNowResponse(
                result.OpenedCount,
                result.ClosedCount,
                runAtUtc),
            HttpContext.TraceIdentifier);
    }
}

public sealed record EducationAccountLifecycleRunNowResponse(
    int OpenedCount,
    int ClosedCount,
    DateTimeOffset RunAtUtc);
