using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.EducationAccountTopUp.Application.EducationAccounts.GetMyEducationAccount;
using Moe.Modules.EducationAccountTopUp.Application.SettlementPreferences;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Api.EService;

[ApiController]
[ApiVersion(1.0)]
[Route("api/eservice/v{version:apiVersion}/my-education-account")]
[Authorize(Policy = AuthorizationPolicies.EServicePortal)]
[EnableCors("EServiceCors")]
public sealed class MyEducationAccountController(
    ICurrentUser currentUser,
    IQueryDispatcher queries,
    ICommandDispatcher commands) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        long? personId = currentUser.PersonId;
        if (!currentUser.IsAuthenticated || personId is null)
        {
            return ApiResponseFactory.Failure(
                EducationAccountErrors.AuthenticatedStudentRequired,
                ApiResponseCodes.Unauthorized,
                HttpContext.TraceIdentifier);
        }

        Result<MyEducationAccountDto> result = await queries.Send(
            new GetMyEducationAccountQuery(personId.Value),
            cancellationToken);

        return ToAccountResponse(result);
    }

    [HttpGet("transactions")]
    [ProducesResponseType(typeof(ApiResponse<MyEducationAccountTransactionsPage>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? category = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        long? personId = currentUser.PersonId;
        if (!currentUser.IsAuthenticated || personId is null)
        {
            return ApiResponseFactory.Failure(
                EducationAccountErrors.AuthenticatedStudentRequired,
                ApiResponseCodes.Unauthorized,
                HttpContext.TraceIdentifier);
        }

        Result<MyEducationAccountTransactionsPage> result = await queries.Send(
            new GetMyEducationAccountTransactionsQuery(personId.Value, page, pageSize, category, sortBy, sortDirection),
            cancellationToken);

        return ToAccountResponse(result);
    }

    [HttpGet("settlement-preference")]
    [ProducesResponseType(typeof(ApiResponse<SettlementPreferenceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSettlementPreference(CancellationToken cancellationToken)
    {
        long? personId = currentUser.PersonId;
        if (!currentUser.IsAuthenticated || personId is null)
        {
            return ApiResponseFactory.Failure(
                EducationAccountErrors.AuthenticatedStudentRequired,
                ApiResponseCodes.Unauthorized,
                HttpContext.TraceIdentifier);
        }

        Result<SettlementPreferenceResponse> result = await queries.Send(
            new GetSettlementPreferenceQuery(personId.Value),
            cancellationToken);

        return ToAccountResponse(result);
    }

    [HttpPut("settlement-preference")]
    [ProducesResponseType(typeof(ApiResponse<SettlementPreferenceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetSettlementPreference(
        [FromBody] SetSettlementPreferenceRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ApiResponseFactory.Failure(
                EducationAccountErrors.InvalidSettlementPreference,
                ApiResponseCodes.BadRequest,
                HttpContext.TraceIdentifier);
        }

        long? personId = currentUser.PersonId;
        if (!currentUser.IsAuthenticated || personId is null)
        {
            return ApiResponseFactory.Failure(
                EducationAccountErrors.AuthenticatedStudentRequired,
                ApiResponseCodes.Unauthorized,
                HttpContext.TraceIdentifier);
        }

        Result<SettlementPreferenceResponse> result = await commands.Send(
            new SetSettlementPreferenceCommand(
                personId.Value,
                request.DestinationTypeCode,
                request.BankName,
                request.BankAccountNumber,
                request.ExpectedUpdatedAtUtc),
            cancellationToken);

        return ToAccountResponse(result);
    }

    private IActionResult ToAccountResponse<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return ApiResponseFactory.Ok(result.Value, HttpContext.TraceIdentifier);
        }

        int statusCode = result.Error == EducationAccountErrors.NotFound
            ? ApiResponseCodes.NotFound
            : result.Error == EducationAccountErrors.AuthenticatedStudentRequired
                ? ApiResponseCodes.Unauthorized
                : result.Error == EducationAccountErrors.SettlementPreferenceConflict
                    ? ApiResponseCodes.Conflict
                    : ApiResponseCodes.BadRequest;

        return ApiResponseFactory.Failure(
            result.Error,
            statusCode,
            HttpContext.TraceIdentifier);
    }
}
