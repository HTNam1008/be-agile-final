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
    public async Task Admin_Can_Update_And_Publish_Course_When_Enrollment_Already_Open()
    {
        string courseCode = $"OPENED-{NewSuffix()}";
        long courseId = await CreateDraftCourseWithFeeAsync(1, courseCode);
        DateTime enrollmentOpenAt = DateTime.UtcNow.AddMinutes(-2);
        DateOnly startDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);

        using HttpRequestMessage update = HqAdminMessage(HttpMethod.Put, $"/api/admin/v1/courses/{courseId}");
        update.Content = JsonContent.Create(new
        {
            courseCode,
            courseName = $"Course {courseCode}",
            description = "Enrollment window already opened",
            startDate,
            endDate = startDate.AddDays(90),
            enrollmentOpenAt,
            enrollmentCloseAt = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddMinutes(-1),
            beforeStartRefundPercentage = 100m,
            afterStartRefundPercentage = 50m
        });
        using HttpResponseMessage updateResponse = await _client.SendAsync(update);
        await AssertStatusAsync(HttpStatusCode.OK, updateResponse);

        using HttpRequestMessage publish = HqAdminMessage(HttpMethod.Post, $"/api/admin/v1/courses/{courseId}/publish");
        using HttpResponseMessage publishResponse = await _client.SendAsync(publish);

        await AssertStatusAsync(HttpStatusCode.OK, publishResponse);
        Assert.Equal(CourseStatusCodes.Published, (await ReadDataElementAsync(publishResponse))
            .GetProperty("courseStatusCode")
            .GetString());
    }

    [Fact]
    public async Task Admin_Cannot_Create_Course_When_Enrollment_Close_Is_In_Past()
    {
        DateOnly startDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14);
        DateTime enrollmentOpenAt = DateTime.UtcNow.AddHours(-2);

        using HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/admin/v1/courses",
            new
            {
                organizationId = 1,
                courseCode = $"CLOSED-{NewSuffix()}",
                courseName = "Closed enrollment window",
                description = "Enrollment close already passed",
                startDate,
                endDate = startDate.AddDays(30),
                enrollmentOpenAt,
                enrollmentCloseAt = DateTime.UtcNow.AddMinutes(-1)
            });

        await AssertStatusAsync(HttpStatusCode.BadRequest, response);
        Assert.Contains("COURSE.ENROLLMENT_DATE_IN_PAST", await response.Content.ReadAsStringAsync());
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

    [Fact]
    public async Task Admin_Can_Add_Student_Without_Plan_And_Student_Selects_Plan_Later()
    {
        StudentLogin login = await CreateStudentAndLoginAsync();
        long courseId = await CreatePublishedCourseAsync(1, $"ADMIN-ADD-{NewSuffix()}");

        using HttpRequestMessage adminAdd = HqAdminMessage(
            HttpMethod.Post,
            $"/api/admin/v1/courses/{courseId}/enrollments");
        adminAdd.Content = JsonContent.Create(new { studentNumber = login.StudentNumber });
        using HttpResponseMessage adminAddResponse = await _client.SendAsync(adminAdd);
        await AssertStatusAsync(HttpStatusCode.Created, adminAddResponse);
        JsonElement adminAddData = await ReadDataElementAsync(adminAddResponse);
        long enrollmentId = adminAddData.GetProperty("courseEnrollmentId").GetInt64();
        Assert.Equal(CourseEnrollmentStatusCodes.PendingPlanSelection, adminAddData.GetProperty("enrollmentStatusCode").GetString());
        Assert.Equal(JsonValueKind.Null, adminAddData.GetProperty("billId").ValueKind);

        using HttpRequestMessage dashboard = new(HttpMethod.Get, "/api/eservice/v1/dashboard");
        dashboard.Headers.Add("X-Test-PersonId", login.PersonId.ToString());
        dashboard.Headers.Add("X-Test-UserAccountId", login.UserAccountId.ToString());
        using HttpResponseMessage dashboardResponse = await _client.SendAsync(dashboard);
        await AssertStatusAsync(HttpStatusCode.OK, dashboardResponse);
        JsonElement course = (await ReadDataElementAsync(dashboardResponse))
            .GetProperty("currentCourses")
            .EnumerateArray()
            .Single(x => x.GetProperty("courseEnrollmentId").GetInt64() == enrollmentId);
        Assert.Equal(CourseEnrollmentStatusCodes.PendingPlanSelection, course.GetProperty("enrollmentStatusCode").GetString());
        Assert.Equal("Choose payment plan", course.GetProperty("enrollmentStatusLabel").GetString());
        Assert.Equal(JsonValueKind.Null, course.GetProperty("coursePaymentPlanId").ValueKind);

        using HttpRequestMessage choosePlan = new(
            HttpMethod.Put,
            $"/api/eservice/v1/course-enrollments/{enrollmentId}/payment-plan");
        choosePlan.Headers.Add("X-Test-PersonId", login.PersonId.ToString());
        choosePlan.Headers.Add("X-Test-UserAccountId", login.UserAccountId.ToString());
        choosePlan.Content = JsonContent.Create(new { coursePaymentPlanId = _fullPaymentPlans[courseId] });
        using HttpResponseMessage choosePlanResponse = await _client.SendAsync(choosePlan);

        await AssertStatusAsync(HttpStatusCode.OK, choosePlanResponse);
        JsonElement choosePlanData = await ReadDataElementAsync(choosePlanResponse);
        Assert.Equal(CourseEnrollmentStatusCodes.PendingPayment, choosePlanData.GetProperty("enrollmentStatusCode").GetString());
        Assert.Equal(27.25m, choosePlanData.GetProperty("outstandingAmount").GetDecimal());
        Assert.Single(choosePlanData.GetProperty("generatedBills").EnumerateArray());
    }

    [Fact]
    public async Task Publishing_Course_Does_Not_Issue_Bill_For_Admin_Added_Student_Waiting_For_Plan()
    {
        StudentLogin login = await CreateStudentAndLoginAsync();
        long courseId = await CreateDraftCourseWithFeeAsync(1, $"PUBLISH-NOPLAN-{NewSuffix()}");

        using HttpRequestMessage adminAdd = HqAdminMessage(
            HttpMethod.Post,
            $"/api/admin/v1/courses/{courseId}/enrollments");
        adminAdd.Content = JsonContent.Create(new { studentNumber = login.StudentNumber });
        using HttpResponseMessage adminAddResponse = await _client.SendAsync(adminAdd);
        await AssertStatusAsync(HttpStatusCode.Created, adminAddResponse);
        long enrollmentId = (await ReadDataElementAsync(adminAddResponse))
            .GetProperty("courseEnrollmentId")
            .GetInt64();

        using HttpRequestMessage publish = HqAdminMessage(HttpMethod.Post, $"/api/admin/v1/courses/{courseId}/publish");
        using HttpResponseMessage publishResponse = await _client.SendAsync(publish);
        await AssertStatusAsync(HttpStatusCode.OK, publishResponse);

        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        CourseEnrollment enrollment = await db.Set<CourseEnrollment>().SingleAsync(x => x.Id == enrollmentId);
        Assert.Equal(CourseEnrollmentStatusCodes.PendingPlanSelection, enrollment.EnrollmentStatusCode);
        Assert.Null(enrollment.CoursePaymentPlanId);
        Assert.Empty(await db.Set<Bill>().Where(x => x.CourseEnrollmentId == enrollmentId).ToArrayAsync());
    }

    [Fact]
    public async Task Student_Dashboard_Summary_And_Courses_Endpoints_Return_Purpose_Built_Payloads()
    {
        StudentLogin login = await CreateStudentAndLoginAsync();
        long courseId = await CreatePublishedCourseAsync(1, $"DASH-SPLIT-{NewSuffix()}");
        using HttpResponseMessage join = await SendEServiceJoinAsync(courseId, login);
        await AssertStatusAsync(HttpStatusCode.Created, join);

        using HttpRequestMessage summary = new(HttpMethod.Get, "/api/eservice/v1/dashboard/summary");
        summary.Headers.Add("X-Test-PersonId", login.PersonId.ToString());
        summary.Headers.Add("X-Test-UserAccountId", login.UserAccountId.ToString());
        using HttpResponseMessage summaryResponse = await _client.SendAsync(summary);
        await AssertStatusAsync(HttpStatusCode.OK, summaryResponse);
        JsonElement summaryData = await ReadDataElementAsync(summaryResponse);
        Assert.True(summaryData.TryGetProperty("student", out _));
        Assert.True(summaryData.TryGetProperty("educationAccount", out _));
        Assert.Equal(1, summaryData.GetProperty("currentCourseCount").GetInt32());
        Assert.False(summaryData.TryGetProperty("currentCourses", out _));
        Assert.False(summaryData.TryGetProperty("publishedCourses", out _));

        using HttpRequestMessage courses = new(HttpMethod.Get, "/api/eservice/v1/dashboard/courses");
        courses.Headers.Add("X-Test-PersonId", login.PersonId.ToString());
        courses.Headers.Add("X-Test-UserAccountId", login.UserAccountId.ToString());
        using HttpResponseMessage coursesResponse = await _client.SendAsync(courses);
        await AssertStatusAsync(HttpStatusCode.OK, coursesResponse);
        JsonElement coursesData = await ReadDataElementAsync(coursesResponse);
        Assert.True(coursesData.TryGetProperty("filters", out _));
        Assert.Single(coursesData.GetProperty("currentCourses").EnumerateArray());
        Assert.True(coursesData.TryGetProperty("publishedCourses", out _));
        Assert.False(coursesData.TryGetProperty("student", out _));
        Assert.False(coursesData.TryGetProperty("educationAccount", out _));
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

        using HttpResponseMessage openAccResponse = await _client.PostAsJsonAsync(
            "/api/admin/v1/education-accounts",
            new
            {
                personId,
                reasonCode = "EXCEPTION",
                remarks = "Open account for test student"
            });
        await AssertStatusAsync(HttpStatusCode.Created, openAccResponse);

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
        return new StudentLogin(resolution.UserAccountId, resolution.PersonId, $"CB-{suffix}");
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
        return (await ReadDataElementAsync(response)).GetProperty(propertyName).GetInt64();
    }

    private static async Task<JsonElement> ReadDataElementAsync(HttpResponseMessage response)
    {
        using Stream stream = await response.Content.ReadAsStreamAsync();
        using JsonDocument document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.GetProperty("data").Clone();
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

    private sealed record StudentLogin(long UserAccountId, long PersonId, string StudentNumber);
}
