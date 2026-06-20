using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Moe.Modules.IdentityPlatform.IGateway.Authentication;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class CourseBillingAuthorizationApiTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task SchoolAdmin_Can_Create_Own_School_Course_But_Not_Other_School_Course()
    {
        string suffix = NewSuffix();

        using HttpResponseMessage ownSchool = await _client.PostAsJsonAsync(
            "/api/admin/v1/courses",
            CreateCourseBody(1, $"OWN-{suffix}"));

        await AssertStatusAsync(HttpStatusCode.Created, ownSchool);

        using HttpResponseMessage otherSchool = await _client.PostAsJsonAsync(
            "/api/admin/v1/courses",
            CreateCourseBody(2, $"DENY-{suffix}"));

        await AssertStatusAsync(HttpStatusCode.Forbidden, otherSchool);
        Assert.Contains("COURSE.ORGANIZATION_FORBIDDEN", await otherSchool.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task HqAdmin_Can_Create_Course_In_Any_School()
    {
        using HttpRequestMessage message = new(HttpMethod.Post, "/api/admin/v1/courses");
        message.Headers.Add("X-Test-Role", "HQ_ADMIN");
        message.Content = JsonContent.Create(CreateCourseBody(2, $"SYS-{NewSuffix()}"));

        using HttpResponseMessage response = await _client.SendAsync(message);

        await AssertStatusAsync(HttpStatusCode.Created, response);
        Assert.Equal(2, await ReadLongAsync(response, "organizationId"));
    }

    [Fact]
    public async Task Student_Can_Self_Join_Own_School_Published_Course_But_Not_Other_School_Course()
    {
        StudentLogin login = await CreateStudentAndLoginAsync();
        long ownCourseId = await CreatePublishedCourseAsync(1, $"JOIN-{NewSuffix()}");
        long otherCourseId = await CreatePublishedCourseAsync(2, $"XJOIN-{NewSuffix()}");

        using HttpResponseMessage ownJoin = await SendEServiceJoinAsync(ownCourseId, login);

        await AssertStatusAsync(HttpStatusCode.Created, ownJoin);
        Assert.Equal(ownCourseId, await ReadLongAsync(ownJoin, "courseId"));

        using HttpResponseMessage otherJoin = await SendEServiceJoinAsync(otherCourseId, login);

        await AssertStatusAsync(HttpStatusCode.Conflict, otherJoin);
        Assert.Contains("COURSE.PERSON_NOT_IN_ORGANIZATION", await otherJoin.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Student_Cannot_Self_Join_Draft_Course()
    {
        StudentLogin login = await CreateStudentAndLoginAsync();
        long draftCourseId = await CreateDraftCourseWithFeeAsync(1, $"DRAFT-{NewSuffix()}");

        using HttpResponseMessage response = await SendEServiceJoinAsync(draftCourseId, login);

        await AssertStatusAsync(HttpStatusCode.Conflict, response);
        Assert.Contains("COURSE.NOT_PUBLISHED", await response.Content.ReadAsStringAsync());
    }

    private async Task<long> CreatePublishedCourseAsync(long organizationId, string courseCode)
    {
        long courseId = await CreateDraftCourseWithFeeAsync(organizationId, courseCode);

        using HttpRequestMessage publish = HqAdminMessage(HttpMethod.Post, $"/api/admin/v1/courses/{courseId}/publish");
        using HttpResponseMessage response = await _client.SendAsync(publish);

        await AssertStatusAsync(HttpStatusCode.OK, response);
        return courseId;
    }

    private async Task<long> CreateDraftCourseWithFeeAsync(long organizationId, string courseCode)
    {
        long feeComponentId = await CreateFeeComponentAsync($"FEE-{courseCode}");

        using HttpRequestMessage createCourse = HqAdminMessage(HttpMethod.Post, "/api/admin/v1/courses");
        createCourse.Content = JsonContent.Create(CreateCourseBody(organizationId, courseCode));
        using HttpResponseMessage courseResponse = await _client.SendAsync(createCourse);
        await AssertStatusAsync(HttpStatusCode.Created, courseResponse);
        long courseId = await ReadLongAsync(courseResponse, "courseId");

        using HttpRequestMessage addFee = HqAdminMessage(HttpMethod.Post, $"/api/admin/v1/courses/{courseId}/fees");
        addFee.Content = JsonContent.Create(new
        {
            feeComponentId,
            feeValue = 25m,
            sequenceNumber = 1
        });

        using HttpResponseMessage feeResponse = await _client.SendAsync(addFee);
        await AssertStatusAsync(HttpStatusCode.Created, feeResponse);
        Assert.True(await ReadLongAsync(feeResponse, "courseFeeId") > 0);

        return courseId;
    }

    private async Task<long> CreateFeeComponentAsync(string componentCode)
    {
        using HttpRequestMessage message = HqAdminMessage(HttpMethod.Post, "/api/admin/v1/fee-components");
        message.Content = JsonContent.Create(new
        {
            componentCode,
            componentName = componentCode,
            componentTypeCode = "TUITION",
            calculationTypeCode = "FIXED",
            isTaxComponent = false,
            isActive = true
        });

        using HttpResponseMessage response = await _client.SendAsync(message);
        await AssertStatusAsync(HttpStatusCode.Created, response);
        return await ReadLongAsync(response, "feeComponentId");
    }

    private async Task<StudentLogin> CreateStudentAndLoginAsync()
    {
        string suffix = NewSuffix();
        string identityNumber = $"H{suffix[..7]}E";

        using HttpResponseMessage createResponse = await _client.PostAsJsonAsync(
            "/api/admin/v1/students",
            new
            {
                identityNumber,
                fullName = "Course Billing Student",
                dateOfBirth = new DateOnly(2008, 5, 12),
                nationalityCode = "SG",
                citizenshipStatusCode = "CITIZEN",
                studentNumber = $"CB-{suffix}",
                academicYear = "2026",
                levelCode = "SEC_4",
                classCode = "4A",
                startDate = new DateOnly(2026, 1, 2),
                isAccountHolder = true
            });

        await AssertStatusAsync(HttpStatusCode.Created, createResponse);
        long personId = await ReadLongAsync(createResponse, "personId");

        using IServiceScope scope = factory.Services.CreateScope();
        IEServiceLoginResolver resolver = scope.ServiceProvider.GetRequiredService<IEServiceLoginResolver>();
        EServiceLoginResolution resolution = await resolver.ResolveAsync(
            new SingpassLoginResult(
                "http://localhost:5156/singpass/v3/fapi",
                $"course-billing-sub-{Guid.NewGuid():N}",
                identityNumber,
                "Course Billing Student",
                "urn:mockpass:loa2",
                "pwd"),
            CancellationToken.None);

        Assert.Equal(personId, resolution.PersonId);
        return new StudentLogin(resolution.UserAccountId, resolution.PersonId);
    }

    private async Task<HttpResponseMessage> SendEServiceJoinAsync(long courseId, StudentLogin login)
    {
        using HttpRequestMessage message = new(HttpMethod.Post, "/api/eservice/v1/course-enrollments");
        message.Headers.Add("X-Test-PersonId", login.PersonId.ToString());
        message.Headers.Add("X-Test-UserAccountId", login.UserAccountId.ToString());
        message.Content = JsonContent.Create(new { courseId });
        return await _client.SendAsync(message);
    }

    private static HttpRequestMessage HqAdminMessage(HttpMethod method, string uri)
    {
        HttpRequestMessage message = new(method, uri);
        message.Headers.Add("X-Test-Role", "HQ_ADMIN");
        return message;
    }

    private static object CreateCourseBody(long organizationId, string courseCode)
        => new
        {
            organizationId,
            courseCode,
            courseName = $"Course {courseCode}",
            description = "Integration course",
            startDate = new DateOnly(2026, 1, 2),
            endDate = new DateOnly(2026, 12, 31),
            enrollmentCloseAt = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc)
        };

    private static async Task<long> ReadLongAsync(HttpResponseMessage response, string propertyName)
    {
        using Stream stream = await response.Content.ReadAsStreamAsync();
        using JsonDocument document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.GetProperty("data").GetProperty(propertyName).GetInt64();
    }

    private static async Task AssertStatusAsync(HttpStatusCode expected, HttpResponseMessage response)
    {
        if (response.StatusCode == expected)
        {
            return;
        }

        string body = await response.Content.ReadAsStringAsync();
        Assert.Fail($"Expected {(int)expected} {expected}, got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
    }

    private static string NewSuffix()
        => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private sealed record StudentLogin(long UserAccountId, long PersonId);
}
