using Microsoft.AspNetCore.Http;
using Moe.Modules.CourseBilling.Application.AdminCourses.Materials;
using Xunit;

namespace Moe.CourseBilling.UnitTests.Application.AdminCourses.Materials;

public sealed class CourseMaterialFileHelperTests
{
    [Theory]
    [InlineData("file.pdf", true)]
    [InlineData("file.docx", true)]
    [InlineData("file.pptx", true)]
    [InlineData("file.png", true)]
    [InlineData("file.jpg", true)]
    [InlineData("file.jpeg", true)]
    [InlineData("file.ppt", false)]
    [InlineData("file.doc", false)]
    [InlineData("file.xlsx", false)]
    [InlineData("file.txt", false)]
    public void IsSupported_AllowsOnlyPreviewableCourseMaterialTypes(string fileName, bool expected)
    {
        FormFile file = new(Stream.Null, 0, 1, "File", fileName);

        Assert.Equal(expected, CourseMaterialFileHelper.IsSupported(file));
    }
}
