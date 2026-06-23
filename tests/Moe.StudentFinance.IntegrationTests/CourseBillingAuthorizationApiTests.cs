using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.IdentityPlatform.IGateway.Authentication;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class CourseBillingAuthorizationApiTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly Dictionary<long, long> _fullPaymentPlans = [];

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
    public async Task Admin_Can_Configure_Refund_Policy_When_Creating_Course()
    {
        DateTime enrollmentOpenAt = DateTime.UtcNow;
        DateOnly startDate = DateOnly.FromDateTime(enrollmentOpenAt).AddDays(30);

        using HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/admin/v1/courses",
            new
            {
                organizationId = 1,
                courseCode = $"REFUND-{NewSuffix()}",
                courseName = "Refund policy course",
                startDate,
                endDate = startDate.AddDays(90),
                enrollmentOpenAt,
                enrollmentCloseAt = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddMinutes(-1),
                beforeStartRefundPercentage = 85m,
                afterStartRefundPercentage = 35m
            });

        await AssertStatusAsync(HttpStatusCode.Created, response);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement data = document.RootElement.GetProperty("data");
        Assert.Equal(85m, data.GetProperty("beforeStartRefundPercentage").GetDecimal());
        Assert.Equal(35m, data.GetProperty("afterStartRefundPercentage").GetDecimal());
    }

    [Fact]
    public async Task Admin_Cannot_Create_Course_With_Invalid_Refund_Percentage()
    {
        DateTime enrollmentOpenAt = DateTime.UtcNow;
        DateOnly startDate = DateOnly.FromDateTime(enrollmentOpenAt).AddDays(30);

        using HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/admin/v1/courses",
            new
            {
                organizationId = 1,
                courseCode = $"BAD-REFUND-{NewSuffix()}",
                courseName = "Invalid refund policy course",
                startDate,
                endDate = startDate.AddDays(90),
                enrollmentOpenAt,
                enrollmentCloseAt = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddMinutes(-1),
                beforeStartRefundPercentage = 101m,
                afterStartRefundPercentage = 50m
            });

        await AssertStatusAsync(HttpStatusCode.BadRequest, response);
    }

    [Fact]
    public async Task Admin_Can_Update_Course_Refund_Policy()
    {
        string courseCode = $"UPDATE-REFUND-{NewSuffix()}";
        using HttpResponseMessage createResponse = await _client.PostAsJsonAsync(
            "/api/admin/v1/courses",
            CreateCourseBody(1, courseCode));
        await AssertStatusAsync(HttpStatusCode.Created, createResponse);
        long courseId = await ReadLongAsync(createResponse, "courseId");

        DateTime enrollmentOpenAt = DateTime.UtcNow;
        DateOnly startDate = DateOnly.FromDateTime(enrollmentOpenAt).AddDays(30);
        using HttpResponseMessage updateResponse = await _client.PutAsJsonAsync(
            $"/api/admin/v1/courses/{courseId}",
            new
            {
                courseCode,
                courseName = "Updated refund policy course",
                startDate,
                endDate = startDate.AddDays(90),
                enrollmentOpenAt,
                enrollmentCloseAt = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddMinutes(-1),
                beforeStartRefundPercentage = 75m,
                afterStartRefundPercentage = 20m
            });

        await AssertStatusAsync(HttpStatusCode.OK, updateResponse);
        using JsonDocument document = JsonDocument.Parse(await updateResponse.Content.ReadAsStringAsync());
        JsonElement data = document.RootElement.GetProperty("data");
        Assert.Equal(75m, data.GetProperty("beforeStartRefundPercentage").GetDecimal());
        Assert.Equal(20m, data.GetProperty("afterStartRefundPercentage").GetDecimal());
    }

    [Fact]
    public async Task Admin_Cannot_Create_Course_With_Past_Course_Dates()
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        DateTime enrollmentOpenAt = DateTime.UtcNow;

        using HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/admin/v1/courses",
            new
            {
                organizationId = 1,
                courseCode = $"PAST-{NewSuffix()}",
                courseName = "Past course",
                description = "Invalid past course",
                startDate = today.AddDays(-2),
                endDate = today.AddDays(-1),
                enrollmentOpenAt,
                enrollmentCloseAt = enrollmentOpenAt.AddHours(1)
            });

        await AssertStatusAsync(HttpStatusCode.BadRequest, response);
        Assert.Contains("COURSE.DATE_IN_PAST", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Admin_Cannot_Close_Enrollment_On_Or_After_Course_Start()
    {
        DateOnly startDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14);
        DateTime enrollmentOpenAt = DateTime.UtcNow;

        using HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/admin/v1/courses",
            new
            {
                organizationId = 1,
                courseCode = $"WINDOW-{NewSuffix()}",
                courseName = "Invalid enrollment window",
                description = "Enrollment closes too late",
                startDate,
                endDate = startDate.AddDays(30),
                enrollmentOpenAt,
                enrollmentCloseAt = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            });

        await AssertStatusAsync(HttpStatusCode.BadRequest, response);
        Assert.Contains("COURSE.ENROLLMENT_MUST_CLOSE_BEFORE_START", await response.Content.ReadAsStringAsync());
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

    [Fact]
    public async Task Student_Cannot_View_Course_Content_Before_Start_Date()
    {
        StudentLogin login = await CreateStudentAndLoginAsync();
        long courseId = await CreatePublishedCourseAsync(1, $"CONTENT-{NewSuffix()}");
        using HttpResponseMessage joinResponse = await SendEServiceJoinAsync(courseId, login);
        await AssertStatusAsync(HttpStatusCode.Created, joinResponse);
        long enrollmentId = await ReadLongAsync(joinResponse, "courseEnrollmentId");

        using HttpRequestMessage request = new(
            HttpMethod.Get,
            $"/api/eservice/v1/course-enrollments/{enrollmentId}/content");
        request.Headers.Add("X-Test-PersonId", login.PersonId.ToString());
        request.Headers.Add("X-Test-UserAccountId", login.UserAccountId.ToString());
        using HttpResponseMessage response = await _client.SendAsync(request);

        await AssertStatusAsync(HttpStatusCode.Conflict, response);
        Assert.Contains("COURSE.CONTENT_NOT_OPEN", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Student_Can_Preview_Cancellation_Before_Course_Start()
    {
        StudentLogin login = await CreateStudentAndLoginAsync();
        long courseId = await CreatePublishedCourseAsync(1, $"CANCEL-{NewSuffix()}");
        using HttpResponseMessage joinResponse = await SendEServiceJoinAsync(courseId, login);
        await AssertStatusAsync(HttpStatusCode.Created, joinResponse);
        long enrollmentId = await ReadLongAsync(joinResponse, "courseEnrollmentId");

        using HttpRequestMessage request = new(
            HttpMethod.Get,
            $"/api/eservice/v1/course-enrollments/{enrollmentId}/cancellation-preview");
        request.Headers.Add("X-Test-PersonId", login.PersonId.ToString());
        request.Headers.Add("X-Test-UserAccountId", login.UserAccountId.ToString());
        using HttpResponseMessage response = await _client.SendAsync(request);

        await AssertStatusAsync(HttpStatusCode.OK, response);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement data = document.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("canCancel").GetBoolean());
        Assert.Equal("BEFORE_COURSE_START", data.GetProperty("policyPeriodCode").GetString());
        Assert.Equal(100m, data.GetProperty("refundPercentage").GetDecimal());
        Assert.Equal(0m, data.GetProperty("refundAmount").GetDecimal());
    }

    [Fact]
    public async Task Student_Can_Cancel_Unpaid_Enrollment_And_Outstanding_Bills()
    {
        StudentLogin login = await CreateStudentAndLoginAsync();
        long courseId = await CreatePublishedCourseAsync(1, $"CANCEL-DO-{NewSuffix()}");
        using HttpResponseMessage joinResponse = await SendEServiceJoinAsync(courseId, login);
        await AssertStatusAsync(HttpStatusCode.Created, joinResponse);
        long enrollmentId = await ReadLongAsync(joinResponse, "courseEnrollmentId");

        using HttpRequestMessage request = new(
            HttpMethod.Post,
            $"/api/eservice/v1/course-enrollments/{enrollmentId}/cancel");
        request.Headers.Add("X-Test-PersonId", login.PersonId.ToString());
        request.Headers.Add("X-Test-UserAccountId", login.UserAccountId.ToString());
        request.Content = JsonContent.Create(new { idempotencyKey = $"cancel-{Guid.NewGuid():N}" });

        using HttpResponseMessage response = await _client.SendAsync(request);

        await AssertStatusAsync(HttpStatusCode.OK, response);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement data = document.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("cancelled").GetBoolean());
        Assert.Equal(CourseEnrollmentStatusCodes.Cancelled, data.GetProperty("enrollmentStatusCode").GetString());
        Assert.Equal(0m, data.GetProperty("refundAmount").GetDecimal());

        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        CourseEnrollment enrollment = await db.Set<CourseEnrollment>().SingleAsync(x => x.Id == enrollmentId);
        Assert.Equal(CourseEnrollmentStatusCodes.Cancelled, enrollment.EnrollmentStatusCode);
        Assert.All(
            await db.Set<Bill>().Where(x => x.CourseEnrollmentId == enrollmentId).ToListAsync(),
            bill => Assert.Equal(BillStatusCodes.Cancelled, bill.BillStatusCode));
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

        using HttpRequestMessage addPlan = HqAdminMessage(
            HttpMethod.Post,
            $"/api/admin/v1/courses/{courseId}/payment-plans");
        addPlan.Content = JsonContent.Create(new
        {
            displayName = "Full payment",
            planTypeCode = "FULL_PAYMENT",
            installmentCount = 1
        });
        using HttpResponseMessage planResponse = await _client.SendAsync(addPlan);
        await AssertStatusAsync(HttpStatusCode.Created, planResponse);
        _fullPaymentPlans[courseId] = await ReadLongAsync(planResponse, "coursePaymentPlanId");

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
        message.Content = JsonContent.Create(new
        {
            courseId,
            coursePaymentPlanId = _fullPaymentPlans[courseId]
        });
        return await _client.SendAsync(message);
    }

    private static HttpRequestMessage HqAdminMessage(HttpMethod method, string uri)
    {
        HttpRequestMessage message = new(method, uri);
        message.Headers.Add("X-Test-Role", "HQ_ADMIN");
        return message;
    }

    private static object CreateCourseBody(long organizationId, string courseCode)
    {
        DateTime enrollmentOpenAt = DateTime.UtcNow;
        DateOnly startDate = DateOnly.FromDateTime(enrollmentOpenAt).AddDays(30);

        return new
        {
            organizationId,
            courseCode,
            courseName = $"Course {courseCode}",
            description = "Integration course",
            startDate,
            endDate = startDate.AddDays(90),
            enrollmentOpenAt,
            enrollmentCloseAt = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddMinutes(-1)
        };
    }

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
