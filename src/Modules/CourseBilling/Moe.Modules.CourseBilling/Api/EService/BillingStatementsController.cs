using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.CourseBilling.Application.BillingStatements;

namespace Moe.Modules.CourseBilling.Api.EService;

[ApiController]
[ApiVersion(1.0)]
[Route("api/eservice/v{version:apiVersion}/billing-statements")]
[Authorize(Policy = AuthorizationPolicies.EServicePortal)]
[EnableCors("EServiceCors")]
public sealed class BillingStatementsController(IQueryDispatcher queries) : ControllerBase
{
    [HttpGet("{year:int}/{month:int}")]
    public async Task<IActionResult> Get(
        int year,
        int month,
        CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(await queries.Send(
            new GetBillingStatementQuery(year, month),
            cancellationToken));
}
