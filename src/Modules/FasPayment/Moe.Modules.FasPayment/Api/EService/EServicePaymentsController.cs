using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.FasPayment.Contracts.Payments;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.FasPayment.Api.EService;

[ApiController]
[ApiVersion(1.0)]
[Route("api/eservice/v{version:apiVersion}/payments")]
[Authorize(Policy = AuthorizationPolicies.EServicePortal)]
[EnableCors("EServiceCors")]
public sealed class EServicePaymentsController(
    MoeDbContext dbContext,
    ICurrentUser currentUser) : ControllerBase
{
    private const string StudentRole = "STUDENT";

    [HttpGet("outstanding-bills")]
    public async Task<IActionResult> GetOutstandingBills(CancellationToken cancellationToken)
    {
        if (!TryGetStudentPersonId(out long personId))
        {
            return ApiResponseFactory.Failure(
                PaymentErrors.AuthenticatedStudentRequired,
                ApiResponseCodes.Forbidden,
                HttpContext.TraceIdentifier);
        }

        OutstandingBillsResponse response = await PaymentSqlReader.ReadOutstandingBillsAsync(
            dbContext,
            personId,
            cancellationToken);

        return ApiResponseFactory.Ok(response, HttpContext.TraceIdentifier);
    }

    [HttpPost("pay")]
    public async Task<IActionResult> PayBill(
        [FromBody] PayBillRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetStudentPersonId(out long personId))
        {
            return ApiResponseFactory.Failure(
                PaymentErrors.AuthenticatedStudentRequired,
                ApiResponseCodes.Forbidden,
                HttpContext.TraceIdentifier);
        }

        PaymentResult result = await PaymentSqlWriter.PayBillAsync(
            dbContext,
            personId,
            currentUser.UserAccountId,
            request,
            cancellationToken);

        if (result.IsFailure)
        {
            return ApiResponseFactory.Failure(
                result.Error,
                result.StatusCode,
                HttpContext.TraceIdentifier);
        }

        return ApiResponseFactory.Created(result.Response, HttpContext.TraceIdentifier, "Payment completed");
    }

    private bool TryGetStudentPersonId(out long personId)
    {
        personId = currentUser.PersonId ?? 0;
        return currentUser.IsAuthenticated
            && currentUser.Portal == PortalCodes.EService
            && currentUser.Roles.Contains(StudentRole)
            && personId > 0;
    }
}
