using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class StripePaymentPlanApiTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task SchoolAdmin_Can_Read_Payment_Plan_Policy()
    {
        using HttpResponseMessage response = await _client.GetAsync("/api/admin/v1/course-payment-plan-policy");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement data = document.RootElement.GetProperty("data");
        Assert.Equal("FULL_PAYMENT", data.GetProperty("planTypeCodes").GetProperty("fullPayment").GetString());
        Assert.Equal("INSTALLMENT", data.GetProperty("planTypeCodes").GetProperty("installment").GetString());
        Assert.Equal(new[] { 2, 3, 6, 9, 12 }, data.GetProperty("allowedInstallmentCounts").EnumerateArray().Select(x => x.GetInt32()).ToArray());
        Assert.Equal(3, data.GetProperty("defaultInstallmentCount").GetInt32());
    }

    [Fact]
    public async Task SchoolAdmin_Can_Create_And_List_Payment_Plans_For_Own_Course()
    {
        long courseId = await CreateCourseAsync(1);

        using HttpResponseMessage create = await _client.PostAsJsonAsync(
            $"/api/admin/v1/courses/{courseId}/payment-plans",
            new { displayName = "Three monthly installments", planTypeCode = "INSTALLMENT", installmentCount = 3 });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        using HttpResponseMessage list = await _client.GetAsync($"/api/admin/v1/courses/{courseId}/payment-plans");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        string body = await list.Content.ReadAsStringAsync();
        Assert.Contains("Three monthly installments", body);
        Assert.Contains("INSTALLMENT", body);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(9)]
    [InlineData(12)]
    public async Task SchoolAdmin_Can_Create_Supported_Installment_Counts(int installmentCount)
    {
        long courseId = await CreateCourseAsync(1);

        using HttpResponseMessage create = await _client.PostAsJsonAsync(
            $"/api/admin/v1/courses/{courseId}/payment-plans",
            new
            {
                displayName = $"{installmentCount} monthly installments",
                planTypeCode = CoursePaymentPlanTypeCodes.Installment,
                installmentCount
            });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
    }

    [Fact]
    public async Task Payment_Plan_Rejects_Unsupported_Installment_Count()
    {
        long courseId = await CreateCourseAsync(1);

        using HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"/api/admin/v1/courses/{courseId}/payment-plans",
            new { displayName = "Five installments", planTypeCode = CoursePaymentPlanTypeCodes.Installment, installmentCount = 5 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refund_Rejects_Invalid_Amount_Before_Calling_Stripe()
    {
        using HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/admin/v1/payments/999999/refunds",
            new { amount = 0m, reason = "Invalid amount" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refund_Returns_NotFound_For_Unknown_Payment()
    {
        using HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/admin/v1/payments/999999/refunds",
            new { amount = 1m, reason = "Unknown payment" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<long> CreateCourseAsync(long organizationId)
    {
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        DateTime enrollmentOpenAt = DateTime.UtcNow;
        DateOnly startDate = DateOnly.FromDateTime(enrollmentOpenAt).AddDays(30);
        using HttpResponseMessage response = await _client.PostAsJsonAsync("/api/admin/v1/courses", new
        {
            organizationId,
            courseCode = $"PAY-{suffix}",
            courseName = $"Payment Course {suffix}",
            description = "Stripe payment integration test",
            startDate,
            endDate = startDate.AddDays(30),
            enrollmentOpenAt,
            enrollmentCloseAt = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddMinutes(-1)
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").GetProperty("courseId").GetInt64();
    }
}
