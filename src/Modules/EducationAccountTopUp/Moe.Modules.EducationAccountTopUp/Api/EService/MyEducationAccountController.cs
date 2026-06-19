using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Api.EService;

[ApiController]
[ApiVersion(1.0)]
[Route("api/eservice/v{version:apiVersion}/my-education-account")]
[Authorize(Policy = AuthorizationPolicies.EServicePortal)]
[EnableCors("EServiceCors")]
public sealed class MyEducationAccountController(
    MoeDbContext dbContext,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.PersonId is null)
        {
            return ApiResponseFactory.Failure(
                new("ACCOUNT.AUTHENTICATED_STUDENT_REQUIRED", "An authenticated student is required."),
                ApiResponseCodes.Unauthorized,
                HttpContext.TraceIdentifier);
        }

        EducationAccount? account = await dbContext.Set<EducationAccount>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PersonId == currentUser.PersonId.Value, cancellationToken);

        if (account is null)
        {
            return ApiResponseFactory.Failure(
                new("ACCOUNT.NOT_FOUND", "No education account was found for the current student."),
                ApiResponseCodes.NotFound,
                HttpContext.TraceIdentifier);
        }

        return ApiResponseFactory.Ok(new
        {
            educationAccountId = account.Id,
            account.PersonId,
            account.AccountNumber,
            currencyCode = CurrencyCodes.SingaporeDollar,
            accountStatusCode = account.StatusCode,
            currentBalance = account.CachedBalance,
            account.OpenedAtUtc,
            openingTypeCode = account.OpeningModeCode,
            openingReason = account.OpeningRemarks,
            account.PendingClosureAtUtc,
            account.ClosedAtUtc,
            transactions = new
            {
                items = Array.Empty<object>(),
                page = 1,
                pageSize = 10,
                totalCount = 0
            }
        }, HttpContext.TraceIdentifier);
    }
}
