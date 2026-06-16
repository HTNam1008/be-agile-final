using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moe.Infrastructure.Shared.Security;

namespace Moe.Modules.EducationAccountTopUp.Api.EService;

[ApiController]
[ApiVersion(1.0)]
[Route("api/eservice/v{version:apiVersion}/my-education-account")]
[Authorize(Policy = AuthorizationPolicies.EServicePortal)]
[EnableCors("EServiceCors")]
public sealed class MyEducationAccountController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return StatusCode(StatusCodes.Status501NotImplemented, new
        {
            code = "TEMPLATE.NOT_IMPLEMENTED",
            message = "Developer 3 implements this query through IEducationAccountReader."
        });
    }
}
