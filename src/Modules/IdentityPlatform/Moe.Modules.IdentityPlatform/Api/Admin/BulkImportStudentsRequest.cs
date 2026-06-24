using Microsoft.AspNetCore.Http;

namespace Moe.Modules.IdentityPlatform.Api.Admin;

public sealed class BulkImportStudentsRequest
{
    public IFormFile? File { get; init; }
}
