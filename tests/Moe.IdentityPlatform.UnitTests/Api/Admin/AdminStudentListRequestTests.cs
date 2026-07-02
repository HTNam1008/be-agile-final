using FluentAssertions;
using Moe.Modules.IdentityPlatform.Api.Admin;
using Xunit;

namespace Moe.IdentityPlatform.UnitTests.Api.Admin;

public sealed class AdminStudentListRequestTests
{
    [Fact]
    public void LevelCode_IsTextQueryParameter_ForSwaggerCommaSeparatedInput()
    {
        typeof(AdminStudentListRequest)
            .GetProperty(nameof(AdminStudentListRequest.LevelCode))!
            .PropertyType
            .Should()
            .Be(typeof(string));
    }

    [Fact]
    public void CitizenshipStatusCode_IsTextQueryParameter()
    {
        typeof(AdminStudentListRequest)
            .GetProperty(nameof(AdminStudentListRequest.CitizenshipStatusCode))!
            .PropertyType
            .Should()
            .Be(typeof(string));
    }
}
