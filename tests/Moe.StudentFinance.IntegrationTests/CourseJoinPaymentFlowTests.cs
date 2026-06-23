using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.FasPayment.Domain.Payments;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class CourseJoinPaymentFlowTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task FullPaymentJoin_CreatesPendingEnrollmentAndOneBillDueImmediately()
    {
        TestStudent student = await CreateStudentAsync(balance: 0m);
        TestCourse course = await CreateCourseAsync(
            fee: 100m,
            plans: [("Full payment", "FULL_PAYMENT", 1)]);

        using HttpResponseMessage response = await JoinCourseAsync(
            student,
            course.CourseId,
            course.PlanIds["FULL_PAYMENT-1"]);

        await AssertStatusAsync(HttpStatusCode.Created, response);
        JsonElement data = await ReadDataAsync(response);
        Assert.Equal("PENDING_PAYMENT", data.GetProperty("enrollmentStatusCode").GetString());
        Assert.Equal(100m, data.GetProperty("outstandingAmount").GetDecimal());
        JsonElement.ArrayEnumerator bills = data.GetProperty("generatedBills").EnumerateArray();
        Assert.Single(bills);
        JsonElement bill = bills.Single();
        Assert.Equal(1, bill.GetProperty("sequenceNumber").GetInt32());
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), ReadDateOnly(bill.GetProperty("currentDueDate")));
        Assert.Equal(100m, bill.GetProperty("outstandingAmount").GetDecimal());
        Assert.Equal("ISSUED", bill.GetProperty("billStatusCode").GetString());
    }

    [Fact]
    public async Task InstallmentJoin_ActivatesEnrollmentAndCreatesThreeMonthlyBillsWithExactTotal()
    {
        TestStudent student = await CreateStudentAsync(balance: 0m);
        TestCourse course = await CreateCourseAsync(
            fee: 100m,
            plans: [("Three monthly payments", "INSTALLMENT", 3)]);

        using HttpResponseMessage response = await JoinCourseAsync(
            student,
            course.CourseId,
            course.PlanIds["INSTALLMENT-3"]);

        await AssertStatusAsync(HttpStatusCode.Created, response);
        JsonElement data = await ReadDataAsync(response);
        Assert.Equal("ACTIVE", data.GetProperty("enrollmentStatusCode").GetString());
        JsonElement[] bills = data.GetProperty("generatedBills").EnumerateArray().ToArray();
        Assert.Equal(3, bills.Length);
        Assert.Equal(100m, bills.Sum(x => x.GetProperty("netPayableAmount").GetDecimal()));
        Assert.Equal([33.34m, 33.33m, 33.33m],
            bills.Select(x => x.GetProperty("netPayableAmount").GetDecimal()).ToArray());

        DateOnly firstOfNextMonth = new(
            DateTime.UtcNow.Year,
            DateTime.UtcNow.Month,
            1);
        firstOfNextMonth = firstOfNextMonth.AddMonths(1);
        Assert.Equal(
            [firstOfNextMonth, firstOfNextMonth.AddMonths(1), firstOfNextMonth.AddMonths(2)],
            bills.Select(x => ReadDateOnly(x.GetProperty("currentDueDate"))).ToArray());
    }

    [Fact]
    public async Task MonthlyBillCheckout_IsOneTimePaymentAndDoesNotCreateStripeSubscriptionSchedule()
    {
        TestStudent student = await CreateStudentAsync(balance: 0m);
        TestCourse course = await CreateCourseAsync(
            fee: 90m,
            plans: [("Three monthly payments", "INSTALLMENT", 3)]);

        using HttpResponseMessage join = await JoinCourseAsync(
            student,
            course.CourseId,
            course.PlanIds["INSTALLMENT-3"]);
        await AssertStatusAsync(HttpStatusCode.Created, join);
        JsonElement firstBill = (await ReadDataAsync(join))
            .GetProperty("generatedBills")
            .EnumerateArray()
            .First();

        using HttpResponseMessage checkoutResponse = await SendStudentAsync(
            student,
            HttpMethod.Post,
            "/api/eservice/v1/payments/checkout-sessions",
            new
            {
                billId = firstBill.GetProperty("billId").GetInt64(),
                coursePaymentPlanId = course.PlanIds["INSTALLMENT-3"]
            });
        await AssertStatusAsync(HttpStatusCode.Created, checkoutResponse);
        JsonElement checkoutData = await ReadDataAsync(checkoutResponse);
        long checkoutId = checkoutData.GetProperty("paymentCheckoutSessionId").GetInt64();

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        PaymentCheckoutSession checkout = await db.Set<PaymentCheckoutSession>()
            .SingleAsync(x => x.Id == checkoutId);

        Assert.Equal(1, checkout.RequiredInstallmentCount);
        Assert.False(checkout.IsInstallment);
        Assert.Equal(30m, checkout.Amount);
    }

    [Fact]
    public async Task JoinRejectsDuplicateEnrollmentAndPlanFromAnotherCourse()
    {
        TestStudent student = await CreateStudentAsync(balance: 0m);
        TestCourse first = await CreateCourseAsync(
            fee: 50m,
            plans: [("Full payment", "FULL_PAYMENT", 1)]);
        TestCourse second = await CreateCourseAsync(
            fee: 75m,
            plans: [("Full payment", "FULL_PAYMENT", 1)]);

        using HttpResponseMessage wrongPlan = await JoinCourseAsync(
            student,
            first.CourseId,
            second.PlanIds["FULL_PAYMENT-1"]);
        await AssertStatusAsync(HttpStatusCode.BadRequest, wrongPlan);
        Assert.Contains("COURSE.PAYMENT_PLAN_NOT_FOUND", await wrongPlan.Content.ReadAsStringAsync());

        using HttpResponseMessage firstJoin = await JoinCourseAsync(
            student,
            first.CourseId,
            first.PlanIds["FULL_PAYMENT-1"]);
        await AssertStatusAsync(HttpStatusCode.Created, firstJoin);

        using HttpResponseMessage duplicate = await JoinCourseAsync(
            student,
            first.CourseId,
            first.PlanIds["FULL_PAYMENT-1"]);
        await AssertStatusAsync(HttpStatusCode.BadRequest, duplicate);
        Assert.Contains("COURSE.ENROLLMENT_DUPLICATE", await duplicate.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ChangePaymentPlan_CancelsOldBillsAndReissuesSequenceFromOne()
    {
        TestStudent student = await CreateStudentAsync(balance: 0m);
        TestCourse course = await CreateCourseAsync(
            fee: 100m,
            plans:
            [
                ("Full payment", "FULL_PAYMENT", 1),
                ("Three monthly payments", "INSTALLMENT", 3)
            ]);

        using HttpResponseMessage join = await JoinCourseAsync(
            student,
            course.CourseId,
            course.PlanIds["FULL_PAYMENT-1"]);
        await AssertStatusAsync(HttpStatusCode.Created, join);
        long enrollmentId = (await ReadDataAsync(join))
            .GetProperty("courseEnrollmentId")
            .GetInt64();

        using HttpResponseMessage change = await SendStudentAsync(
            student,
            HttpMethod.Put,
            $"/api/eservice/v1/course-enrollments/{enrollmentId}/payment-plan",
            new { coursePaymentPlanId = course.PlanIds["INSTALLMENT-3"] });
        await AssertStatusAsync(HttpStatusCode.OK, change);
        JsonElement data = await ReadDataAsync(change);
        Assert.Equal("ACTIVE", data.GetProperty("enrollmentStatusCode").GetString());
        JsonElement[] generated = data.GetProperty("generatedBills").EnumerateArray().ToArray();
        Assert.Equal([1, 2, 3],
            generated.Select(x => x.GetProperty("sequenceNumber").GetInt32()).ToArray());

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        Bill[] bills = await db.Set<Bill>()
            .Where(x => x.CourseEnrollmentId == enrollmentId)
            .OrderBy(x => x.Id)
            .ToArrayAsync();
        Assert.Equal(4, bills.Length);
        Assert.Equal("CANCELLED", bills[0].BillStatusCode);
        Assert.Equal([1, 2, 3], bills.Skip(1).Select(x => x.SequenceNumber).ToArray());

        var index = db.Model.FindEntityType(typeof(Bill))!
            .GetIndexes()
            .Single(x => x.Properties.Select(p => p.Name)
                .SequenceEqual([nameof(Bill.CourseEnrollmentId), nameof(Bill.SequenceNumber)]));
        Assert.Equal("[BillStatusCode] <> 'CANCELLED'", index.GetFilter());
    }

    [Fact]
    public async Task EducationAccountOnlyPayment_DebitsBalanceAndSettlesBillAndEnrollment()
    {
        TestStudent student = await CreateStudentAsync(balance: 150m);
        TestCourse course = await CreateCourseAsync(
            fee: 100m,
            plans: [("Full payment", "FULL_PAYMENT", 1)]);
        await JoinSuccessfullyAsync(student, course, "FULL_PAYMENT-1");
        StatementInfo statement = await GetStatementAsync(student, DateTime.UtcNow.Year, DateTime.UtcNow.Month);

        JsonElement preview = await PreviewPaymentAsync(student, statement.StatementId);
        Assert.Equal(150m, preview.GetProperty("educationAccountCurrentBalance").GetDecimal());
        Assert.Equal(100m, preview.GetProperty("educationAccountAmount").GetDecimal());
        Assert.Equal(0m, preview.GetProperty("onlinePaymentAmount").GetDecimal());
        Assert.Equal("EDUCATION_ACCOUNT_ONLY", preview.GetProperty("recommendedFundingOptionCode").GetString());
        JsonElement educationOnly = preview.GetProperty("fundingOptions").EnumerateArray()
            .Single(x => x.GetProperty("fundingOptionCode").GetString() == "EDUCATION_ACCOUNT_ONLY");
        Assert.True(educationOnly.GetProperty("isAvailable").GetBoolean());
        Assert.Equal(100m, educationOnly.GetProperty("educationAccountAmount").GetDecimal());
        JsonElement onlineOnly = preview.GetProperty("fundingOptions").EnumerateArray()
            .Single(x => x.GetProperty("fundingOptionCode").GetString() == "ONLINE_ONLY");
        Assert.True(onlineOnly.GetProperty("isAvailable").GetBoolean());
        Assert.Equal(100m, onlineOnly.GetProperty("onlinePaymentAmount").GetDecimal());

        using HttpResponseMessage pay = await PayStatementAsync(student, statement.StatementId);
        await AssertStatusAsync(HttpStatusCode.Created, pay);
        JsonElement paymentResponse = await ReadDataAsync(pay);
        Assert.Equal("SUCCESSFUL", paymentResponse.GetProperty("paymentStatusCode").GetString());
        Assert.Equal(JsonValueKind.Null, paymentResponse.GetProperty("checkoutUrl").ValueKind);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        EducationAccount account = await db.Set<EducationAccount>()
            .SingleAsync(x => x.PersonId == student.PersonId);
        AccountTransaction transaction = await db.Set<AccountTransaction>()
            .SingleAsync(x => x.EducationAccountId == account.Id);
        Bill bill = await db.Set<Bill>().SingleAsync(x => x.CourseEnrollmentId == statement.EnrollmentId);
        CourseEnrollment enrollment = await db.Set<CourseEnrollment>().SingleAsync(x => x.Id == statement.EnrollmentId);

        Assert.Equal(50m, account.CachedBalance);
        Assert.Equal(-100m, transaction.Amount);
        Assert.Equal(50m, transaction.BalanceAfter);
        Assert.Equal("PAID", bill.BillStatusCode);
        Assert.Equal(0m, bill.OutstandingAmount);
        Assert.Equal("PAID_IN_FULL", enrollment.EnrollmentStatusCode);
    }

    [Fact]
    public async Task CancelPaidEnrollment_RefundsEducationAccountPaymentToOriginalSource()
    {
        TestStudent student = await CreateStudentAsync(balance: 150m);
        TestCourse course = await CreateCourseAsync(
            fee: 100m,
            plans: [("Full payment", "FULL_PAYMENT", 1)]);
        await JoinSuccessfullyAsync(student, course, "FULL_PAYMENT-1");
        StatementInfo statement = await GetStatementAsync(student, DateTime.UtcNow.Year, DateTime.UtcNow.Month);

        using HttpResponseMessage pay = await PayStatementAsync(
            student,
            statement.StatementId,
            "EDUCATION_ACCOUNT_ONLY");
        await AssertStatusAsync(HttpStatusCode.Created, pay);

        using HttpResponseMessage cancel = await SendStudentAsync(
            student,
            HttpMethod.Post,
            $"/api/eservice/v1/course-enrollments/{statement.EnrollmentId}/cancel",
            new { idempotencyKey = $"cancel-ea-{Guid.NewGuid():N}" });

        await AssertStatusAsync(HttpStatusCode.OK, cancel);
        JsonElement cancellation = await ReadDataAsync(cancel);
        Assert.Equal("REFUNDED", cancellation.GetProperty("enrollmentStatusCode").GetString());
        Assert.Equal(100m, cancellation.GetProperty("refundAmount").GetDecimal());
        Assert.Equal(100m, cancellation.GetProperty("educationAccountRefundAmount").GetDecimal());
        Assert.Equal(0m, cancellation.GetProperty("onlineRefundAmount").GetDecimal());

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        EducationAccount account = await db.Set<EducationAccount>()
            .SingleAsync(x => x.PersonId == student.PersonId);
        AccountTransaction[] transactions = await db.Set<AccountTransaction>()
            .Where(x => x.EducationAccountId == account.Id)
            .OrderBy(x => x.Id)
            .ToArrayAsync();
        CourseEnrollment enrollment = await db.Set<CourseEnrollment>()
            .SingleAsync(x => x.Id == statement.EnrollmentId);

        Assert.Equal(150m, account.CachedBalance);
        Assert.Equal([-100m, 100m], transactions.Select(x => x.Amount).ToArray());
        Assert.Equal([50m, 150m], transactions.Select(x => x.BalanceAfter).ToArray());
        Assert.Equal("REFUNDED", enrollment.EnrollmentStatusCode);
    }

    [Fact]
    public async Task OnlineOnlyPayment_DoesNotReserveEducationAccount()
    {
        TestStudent student = await CreateStudentAsync(balance: 150m);
        TestCourse course = await CreateCourseAsync(
            fee: 100m,
            plans: [("Full payment", "FULL_PAYMENT", 1)]);
        await JoinSuccessfullyAsync(student, course, "FULL_PAYMENT-1");
        StatementInfo statement = await GetStatementAsync(student, DateTime.UtcNow.Year, DateTime.UtcNow.Month);

        using HttpResponseMessage pay = await PayStatementAsync(
            student,
            statement.StatementId,
            "ONLINE_ONLY");
        await AssertStatusAsync(HttpStatusCode.Created, pay);
        JsonElement payment = await ReadDataAsync(pay);

        Assert.Equal(0m, payment.GetProperty("educationAccountAmount").GetDecimal());
        Assert.Equal(100m, payment.GetProperty("onlinePaymentAmount").GetDecimal());
        Assert.Equal(JsonValueKind.String, payment.GetProperty("checkoutUrl").ValueKind);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        long educationAccountId = await db.Set<EducationAccount>()
            .Where(x => x.PersonId == student.PersonId)
            .Select(x => x.Id)
            .SingleAsync();
        Assert.Empty(await db.Set<AccountHold>()
            .Where(x => x.EducationAccountId == educationAccountId)
            .ToArrayAsync());
    }

    [Fact]
    public async Task RefundedStatementPaymentWebhook_UsesPaymentAllocationsInsteadOfZeroBillId()
    {
        TestStudent student = await CreateStudentAsync(balance: 0m);
        TestCourse course = await CreateCourseAsync(
            fee: 100m,
            plans: [("Full payment", "FULL_PAYMENT", 1)]);
        await JoinSuccessfullyAsync(student, course, "FULL_PAYMENT-1");
        StatementInfo statement = await GetStatementAsync(student, DateTime.UtcNow.Year, DateTime.UtcNow.Month);

        using HttpResponseMessage pay = await PayStatementAsync(
            student,
            statement.StatementId,
            "ONLINE_ONLY");
        await AssertStatusAsync(HttpStatusCode.Created, pay);
        JsonElement payment = await ReadDataAsync(pay);
        long checkoutId = ReadCheckoutId(payment.GetProperty("checkoutUrl").GetString()!);
        long paymentId = payment.GetProperty("paymentId").GetInt64();

        await PostWebhookAsync("success", checkoutId, 10000, $"evt_success_{Guid.NewGuid():N}");

        using HttpResponseMessage cancel = await SendStudentAsync(
            student,
            HttpMethod.Post,
            $"/api/eservice/v1/course-enrollments/{statement.EnrollmentId}/cancel",
            new { idempotencyKey = $"cancel-online-{Guid.NewGuid():N}" });
        await AssertStatusAsync(HttpStatusCode.OK, cancel);

        await PostWebhookAsync("refund", checkoutId, 10000, $"evt_refund_{Guid.NewGuid():N}");

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        Payment refundedPayment = await db.Set<Payment>().SingleAsync(x => x.Id == paymentId);
        CourseEnrollment enrollment = await db.Set<CourseEnrollment>()
            .SingleAsync(x => x.Id == statement.EnrollmentId);

        Assert.Equal(0, refundedPayment.BillId);
        Assert.Equal(PaymentStatusCodes.Refunded, refundedPayment.PaymentStatusCode);
        Assert.Equal(CourseEnrollmentStatusCodes.Refunded, enrollment.EnrollmentStatusCode);
    }

    [Fact]
    public async Task EducationAccountOnlyPayment_RejectsInsufficientBalance()
    {
        TestStudent student = await CreateStudentAsync(balance: 40m);
        TestCourse course = await CreateCourseAsync(
            fee: 100m,
            plans: [("Full payment", "FULL_PAYMENT", 1)]);
        await JoinSuccessfullyAsync(student, course, "FULL_PAYMENT-1");
        StatementInfo statement = await GetStatementAsync(student, DateTime.UtcNow.Year, DateTime.UtcNow.Month);

        using HttpResponseMessage pay = await PayStatementAsync(
            student,
            statement.StatementId,
            "EDUCATION_ACCOUNT_ONLY");

        await AssertStatusAsync(HttpStatusCode.Conflict, pay);
        Assert.Contains("PAYMENT.INSUFFICIENT_BALANCE", await pay.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SplitPayment_ResumesExistingCheckout_AndSuccessCapturesEaExactlyOnce()
    {
        TestStudent student = await CreateStudentAsync(balance: 40m);
        TestCourse course = await CreateCourseAsync(
            fee: 100m,
            plans: [("Full payment", "FULL_PAYMENT", 1)]);
        await JoinSuccessfullyAsync(student, course, "FULL_PAYMENT-1");
        StatementInfo statement = await GetStatementAsync(student, DateTime.UtcNow.Year, DateTime.UtcNow.Month);

        JsonElement preview = await PreviewPaymentAsync(student, statement.StatementId);
        Assert.Equal(40m, preview.GetProperty("educationAccountAmount").GetDecimal());
        Assert.Equal(60m, preview.GetProperty("onlinePaymentAmount").GetDecimal());
        Assert.Equal("EDUCATION_ACCOUNT_THEN_ONLINE", preview.GetProperty("recommendedFundingOptionCode").GetString());
        JsonElement splitOption = preview.GetProperty("fundingOptions").EnumerateArray()
            .Single(x => x.GetProperty("fundingOptionCode").GetString() == "EDUCATION_ACCOUNT_THEN_ONLINE");
        Assert.True(splitOption.GetProperty("isAvailable").GetBoolean());
        Assert.Equal(40m, splitOption.GetProperty("educationAccountAmount").GetDecimal());
        Assert.Equal(60m, splitOption.GetProperty("onlinePaymentAmount").GetDecimal());

        using HttpResponseMessage firstPay = await PayStatementAsync(student, statement.StatementId);
        await AssertStatusAsync(HttpStatusCode.Created, firstPay);
        JsonElement firstPayment = await ReadDataAsync(firstPay);
        long paymentId = firstPayment.GetProperty("paymentId").GetInt64();
        long checkoutId = ReadCheckoutId(firstPayment.GetProperty("checkoutUrl").GetString()!);

        using HttpResponseMessage parallelPay = await PayStatementAsync(student, statement.StatementId);
        await AssertStatusAsync(HttpStatusCode.Created, parallelPay);
        JsonElement resumedPayment = await ReadDataAsync(parallelPay);
        Assert.Equal(paymentId, resumedPayment.GetProperty("paymentId").GetInt64());
        Assert.Equal(firstPayment.GetProperty("checkoutUrl").GetString(),
            resumedPayment.GetProperty("checkoutUrl").GetString());
        Assert.True(resumedPayment.GetProperty("resumed").GetBoolean());

        await Task.WhenAll(
            PostWebhookAsync("success", checkoutId, 6000, "evt_split_success"),
            PostWebhookAsync("success", checkoutId, 6000, "evt_split_success"));
        await PostWebhookAsync("failure", checkoutId, 6000, "evt_split_late_failure");

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        EducationAccount account = await db.Set<EducationAccount>()
            .SingleAsync(x => x.PersonId == student.PersonId);
        Payment payment = await db.Set<Payment>().SingleAsync(x => x.Id == paymentId);
        PaymentPart[] parts = await db.Set<PaymentPart>()
            .Where(x => x.PaymentId == paymentId)
            .OrderBy(x => x.SequenceNumber)
            .ToArrayAsync();
        AccountTransaction[] transactions = await db.Set<AccountTransaction>()
            .Where(x => x.EducationAccountId == account.Id)
            .ToArrayAsync();
        AccountHold hold = await db.Set<AccountHold>()
            .SingleAsync(x => x.PaymentPartId == parts[0].Id);
        Bill bill = await db.Set<Bill>().SingleAsync(x => x.CourseEnrollmentId == statement.EnrollmentId);

        Assert.Equal(0m, account.CachedBalance);
        Assert.Single(transactions);
        Assert.Equal(-40m, transactions[0].Amount);
        Assert.Equal("CAPTURED", hold.HoldStatusCode);
        Assert.Equal("SUCCESSFUL", payment.PaymentStatusCode);
        Assert.Equal(40m, payment.EducationAccountAmount);
        Assert.Equal(60m, payment.OnlinePaymentAmount);
        Assert.Equal(["CAPTURED", "SUCCESSFUL"], parts.Select(x => x.PartStatusCode).ToArray());
        Assert.Equal("PAID", bill.BillStatusCode);
    }

    [Fact]
    public async Task CancelPendingCheckout_ReleasesEducationAccountHold_AndAllowsNewPayment()
    {
        TestStudent student = await CreateStudentAsync(balance: 40m);
        TestCourse course = await CreateCourseAsync(
            fee: 100m,
            plans: [("Full payment", "FULL_PAYMENT", 1)]);
        await JoinSuccessfullyAsync(student, course, "FULL_PAYMENT-1");
        StatementInfo statement = await GetStatementAsync(student, DateTime.UtcNow.Year, DateTime.UtcNow.Month);

        using HttpResponseMessage firstResponse = await PayStatementAsync(student, statement.StatementId);
        await AssertStatusAsync(HttpStatusCode.Created, firstResponse);
        JsonElement firstPayment = await ReadDataAsync(firstResponse);
        long firstPaymentId = firstPayment.GetProperty("paymentId").GetInt64();

        using HttpResponseMessage cancelResponse = await SendStudentAsync(
            student,
            HttpMethod.Post,
            $"/api/eservice/v1/billing-statements/{statement.StatementId}/payments/{firstPaymentId}/cancel");
        await AssertStatusAsync(HttpStatusCode.OK, cancelResponse);

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
            Payment cancelled = await db.Set<Payment>().SingleAsync(x => x.Id == firstPaymentId);
            long educationPartId = await db.Set<PaymentPart>()
                .Where(part => part.PaymentId == firstPaymentId &&
                    part.PaymentMethodCode == PaymentMethodCodes.EducationAccount)
                .Select(part => part.Id)
                .SingleAsync();
            AccountHold hold = await db.Set<AccountHold>()
                .SingleAsync(x => x.PaymentPartId == educationPartId);
            Assert.Equal(PaymentStatusCodes.Cancelled, cancelled.PaymentStatusCode);
            Assert.Equal("RELEASED", hold.HoldStatusCode);
        }

        using HttpResponseMessage retryResponse = await PayStatementAsync(student, statement.StatementId);
        await AssertStatusAsync(HttpStatusCode.Created, retryResponse);
        JsonElement retryPayment = await ReadDataAsync(retryResponse);
        Assert.NotEqual(firstPaymentId, retryPayment.GetProperty("paymentId").GetInt64());
        Assert.False(retryPayment.GetProperty("resumed").GetBoolean());
    }

    [Fact]
    public async Task ExpiredStripeCheckout_ReleasesEducationAccountHold_AndAllowsRetry()
    {
        TestStudent student = await CreateStudentAsync(balance: 40m);
        TestCourse course = await CreateCourseAsync(
            fee: 100m,
            plans: [("Full payment", "FULL_PAYMENT", 1)]);
        await JoinSuccessfullyAsync(student, course, "FULL_PAYMENT-1");
        StatementInfo statement = await GetStatementAsync(student, DateTime.UtcNow.Year, DateTime.UtcNow.Month);

        using HttpResponseMessage firstResponse = await PayStatementAsync(student, statement.StatementId);
        await AssertStatusAsync(HttpStatusCode.Created, firstResponse);
        JsonElement firstPayment = await ReadDataAsync(firstResponse);
        long firstPaymentId = firstPayment.GetProperty("paymentId").GetInt64();
        long checkoutId = firstPayment.GetProperty("paymentCheckoutSessionId").GetInt64();

        await PostWebhookAsync(
            "expired",
            checkoutId,
            0,
            $"evt_expired_{Guid.NewGuid():N}");

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
            Payment expired = await db.Set<Payment>().SingleAsync(x => x.Id == firstPaymentId);
            PaymentCheckoutSession checkout = await db.Set<PaymentCheckoutSession>()
                .SingleAsync(x => x.Id == checkoutId);
            Assert.Equal(PaymentStatusCodes.Expired, expired.PaymentStatusCode);
            Assert.Equal(CheckoutStatusCodes.Expired, checkout.CheckoutStatusCode);
        }

        using HttpResponseMessage retryResponse = await PayStatementAsync(student, statement.StatementId);
        await AssertStatusAsync(HttpStatusCode.Created, retryResponse);
        Assert.NotEqual(
            firstPaymentId,
            (await ReadDataAsync(retryResponse)).GetProperty("paymentId").GetInt64());
    }

    [Fact]
    public async Task FailedStripePayment_ReleasesEa_AllowsRetry_AndCanDeferBill()
    {
        TestStudent student = await CreateStudentAsync(balance: 40m);
        TestCourse course = await CreateCourseAsync(
            fee: 100m,
            plans: [("Full payment", "FULL_PAYMENT", 1)]);
        await JoinSuccessfullyAsync(student, course, "FULL_PAYMENT-1");
        StatementInfo statement = await GetStatementAsync(student, DateTime.UtcNow.Year, DateTime.UtcNow.Month);

        using HttpResponseMessage firstPayResponse = await PayStatementAsync(student, statement.StatementId);
        await AssertStatusAsync(HttpStatusCode.Created, firstPayResponse);
        JsonElement firstPay = await ReadDataAsync(firstPayResponse);
        long firstCheckoutId = ReadCheckoutId(firstPay.GetProperty("checkoutUrl").GetString()!);
        await PostWebhookAsync("failure", firstCheckoutId, 6000, $"evt_fail_{Guid.NewGuid():N}");

        await AssertFailedAttemptReleasedAsync(student.PersonId, firstPay.GetProperty("paymentId").GetInt64(), 40m);

        using HttpResponseMessage retryResponse = await PayStatementAsync(student, statement.StatementId);
        await AssertStatusAsync(HttpStatusCode.Created, retryResponse);
        JsonElement retry = await ReadDataAsync(retryResponse);
        Assert.NotEqual(firstPay.GetProperty("paymentId").GetInt64(), retry.GetProperty("paymentId").GetInt64());
        Assert.Equal(40m, retry.GetProperty("educationAccountAmount").GetDecimal());
        Assert.Equal(60m, retry.GetProperty("onlinePaymentAmount").GetDecimal());

        long retryCheckoutId = ReadCheckoutId(retry.GetProperty("checkoutUrl").GetString()!);
        await PostWebhookAsync("failure", retryCheckoutId, 6000, $"evt_retry_fail_{Guid.NewGuid():N}");

        using HttpResponseMessage defer = await SendStudentAsync(
            student,
            HttpMethod.Post,
            $"/api/eservice/v1/billing-statements/{statement.StatementId}/defer",
            new { failedPaymentId = retry.GetProperty("paymentId").GetInt64() });
        await AssertStatusAsync(HttpStatusCode.OK, defer);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        EducationAccount account = await db.Set<EducationAccount>()
            .SingleAsync(x => x.PersonId == student.PersonId);
        Bill bill = await db.Set<Bill>().SingleAsync(x => x.CourseEnrollmentId == statement.EnrollmentId);
        AccountHold[] activeHolds = await db.Set<AccountHold>()
            .Where(x => x.EducationAccountId == account.Id && x.HoldStatusCode == "RESERVED")
            .ToArrayAsync();

        Assert.Equal(40m, account.CachedBalance);
        Assert.Empty(await db.Set<AccountTransaction>()
            .Where(x => x.EducationAccountId == account.Id)
            .ToArrayAsync());
        Assert.Empty(activeHolds);
        Assert.Equal(1, bill.DeferralCount);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(1), bill.CurrentDueDate);
        Assert.Equal("DEFERRED", bill.BillStatusCode);
        Assert.Equal(100m, bill.OutstandingAmount);
    }

    private async Task AssertFailedAttemptReleasedAsync(long personId, long paymentId, decimal expectedBalance)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        EducationAccount account = await db.Set<EducationAccount>().SingleAsync(x => x.PersonId == personId);
        Payment payment = await db.Set<Payment>().SingleAsync(x => x.Id == paymentId);
        PaymentPart eaPart = await db.Set<PaymentPart>()
            .SingleAsync(x => x.PaymentId == paymentId && x.PaymentMethodCode == "EDUCATION_ACCOUNT");
        AccountHold hold = await db.Set<AccountHold>().SingleAsync(x => x.Id == eaPart.AccountHoldId);

        Assert.Equal(expectedBalance, account.CachedBalance);
        Assert.Equal("FAILED", payment.PaymentStatusCode);
        Assert.Equal("RELEASED", eaPart.PartStatusCode);
        Assert.Equal("RELEASED", hold.HoldStatusCode);
        Assert.False(await db.Set<AccountTransaction>().AnyAsync(x => x.EducationAccountId == account.Id));
    }

    private async Task<TestStudent> CreateStudentAsync(decimal balance)
    {
        string suffix = NewSuffix();
        using HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/admin/v1/students",
            new
            {
                schoolName = (string?)null,
                identityNumber = $"J{suffix[..7]}T",
                fullName = $"Payment Test Student {suffix}",
                dateOfBirth = new DateOnly(2008, 5, 12),
                nationalityCode = "SG",
                citizenshipStatusCode = "CITIZEN",
                studentNumber = $"PAY-{suffix}",
                academicYear = "2026",
                levelCode = "SEC_4",
                classCode = "4A",
                startDate = new DateOnly(2026, 1, 2),
                email = (string?)null,
                mobile = (string?)null,
                address = (string?)null,
                isAccountHolder = true
            });
        await AssertStatusAsync(HttpStatusCode.Created, response);
        JsonElement data = await ReadDataAsync(response);
        long personId = data.GetProperty("personId").GetInt64();

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        EducationAccount account = await db.Set<EducationAccount>().SingleAsync(x => x.PersonId == personId);
        account.UpdateBalance(balance);
        await db.SaveChangesAsync();

        return new TestStudent(personId, 1003);
    }

    private async Task<TestCourse> CreateCourseAsync(
        decimal fee,
        IReadOnlyCollection<(string Name, string Type, int Count)> plans)
    {
        string suffix = NewSuffix();
        long feeComponentId = await CreateFeeComponentAsync($"PAYFEE-{suffix}");
        DateTime enrollmentOpenAt = DateTime.UtcNow;
        DateOnly startDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);

        using HttpRequestMessage createCourse = AdminMessage(HttpMethod.Post, "/api/admin/v1/courses", new
        {
            organizationId = 1,
            courseCode = $"PAY-{suffix}",
            courseName = $"Payment Flow Course {suffix}",
            description = "Join and payment integration test",
            startDate,
            endDate = startDate.AddDays(90),
            enrollmentOpenAt,
            enrollmentCloseAt = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddMinutes(-1)
        });
        using HttpResponseMessage courseResponse = await _client.SendAsync(createCourse);
        await AssertStatusAsync(HttpStatusCode.Created, courseResponse);
        long courseId = (await ReadDataAsync(courseResponse)).GetProperty("courseId").GetInt64();

        using HttpRequestMessage addFee = AdminMessage(
            HttpMethod.Post,
            $"/api/admin/v1/courses/{courseId}/fees",
            new { feeComponentId, feeValue = fee, sequenceNumber = 1 });
        using HttpResponseMessage feeResponse = await _client.SendAsync(addFee);
        await AssertStatusAsync(HttpStatusCode.Created, feeResponse);

        Dictionary<string, long> planIds = [];
        foreach ((string name, string type, int count) in plans)
        {
            using HttpRequestMessage addPlan = AdminMessage(
                HttpMethod.Post,
                $"/api/admin/v1/courses/{courseId}/payment-plans",
                new { displayName = name, planTypeCode = type, installmentCount = count });
            using HttpResponseMessage planResponse = await _client.SendAsync(addPlan);
            await AssertStatusAsync(HttpStatusCode.Created, planResponse);
            planIds[$"{type}-{count}"] =
                (await ReadDataAsync(planResponse)).GetProperty("coursePaymentPlanId").GetInt64();
        }

        using HttpRequestMessage publish = AdminMessage(
            HttpMethod.Post,
            $"/api/admin/v1/courses/{courseId}/publish");
        using HttpResponseMessage publishResponse = await _client.SendAsync(publish);
        await AssertStatusAsync(HttpStatusCode.OK, publishResponse);
        return new TestCourse(courseId, planIds);
    }

    private async Task<long> CreateFeeComponentAsync(string code)
    {
        using HttpRequestMessage request = AdminMessage(HttpMethod.Post, "/api/admin/v1/fee-components", new
        {
            componentCode = code,
            componentName = code,
            componentTypeCode = "TUITION",
            calculationTypeCode = "FIXED",
            isTaxComponent = false,
            isActive = true
        });
        using HttpResponseMessage response = await _client.SendAsync(request);
        await AssertStatusAsync(HttpStatusCode.Created, response);
        return (await ReadDataAsync(response)).GetProperty("feeComponentId").GetInt64();
    }

    private async Task JoinSuccessfullyAsync(TestStudent student, TestCourse course, string planKey)
    {
        using HttpResponseMessage response = await JoinCourseAsync(
            student,
            course.CourseId,
            course.PlanIds[planKey]);
        await AssertStatusAsync(HttpStatusCode.Created, response);
    }

    private Task<HttpResponseMessage> JoinCourseAsync(
        TestStudent student,
        long courseId,
        long planId)
        => SendStudentAsync(
            student,
            HttpMethod.Post,
            "/api/eservice/v1/course-enrollments",
            new { courseId, coursePaymentPlanId = planId });

    private async Task<StatementInfo> GetStatementAsync(
        TestStudent student,
        int year,
        int month)
    {
        using HttpResponseMessage response = await SendStudentAsync(
            student,
            HttpMethod.Get,
            $"/api/eservice/v1/billing-statements/{year}/{month}");
        await AssertStatusAsync(HttpStatusCode.OK, response);
        JsonElement data = await ReadDataAsync(response);
        JsonElement item = data.GetProperty("items").EnumerateArray().Single();
        long billId = item.GetProperty("billId").GetInt64();

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        long enrollmentId = await db.Set<Bill>()
            .Where(x => x.Id == billId)
            .Select(x => x.CourseEnrollmentId)
            .SingleAsync();
        return new StatementInfo(
            data.GetProperty("billingStatementId").GetInt64(),
            enrollmentId);
    }

    private async Task<JsonElement> PreviewPaymentAsync(TestStudent student, long statementId)
    {
        using HttpResponseMessage response = await SendStudentAsync(
            student,
            HttpMethod.Post,
            $"/api/eservice/v1/billing-statements/{statementId}/payment-preview");
        await AssertStatusAsync(HttpStatusCode.OK, response);
        return await ReadDataAsync(response);
    }

    private Task<HttpResponseMessage> PayStatementAsync(TestStudent student, long statementId)
        => PayStatementAsync(
            student,
            statementId,
            "EDUCATION_ACCOUNT_THEN_ONLINE");

    private Task<HttpResponseMessage> PayStatementAsync(
        TestStudent student,
        long statementId,
        string fundingOptionCode)
        => SendStudentAsync(
            student,
            HttpMethod.Post,
            $"/api/eservice/v1/billing-statements/{statementId}/payments",
            new
            {
                idempotencyKey = $"test-statement-{statementId}-{Guid.NewGuid():N}",
                fundingOptionCode
            });

    private async Task PostWebhookAsync(
        string kind,
        long checkoutId,
        long amountMinor,
        string eventId)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/webhooks/stripe");
        request.Headers.Add("Stripe-Signature", "test-signature");
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            kind,
            checkoutId,
            amountMinor,
            eventId,
            createdAtUtc = DateTime.UtcNow
        }), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await _client.SendAsync(request);
        await AssertStatusAsync(HttpStatusCode.OK, response);
    }

    private async Task<HttpResponseMessage> SendStudentAsync(
        TestStudent student,
        HttpMethod method,
        string uri,
        object? body = null)
    {
        using HttpRequestMessage request = new(method, uri);
        request.Headers.Add("X-Test-PersonId", student.PersonId.ToString());
        request.Headers.Add("X-Test-UserAccountId", student.UserAccountId.ToString());
        if (body is not null) request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    private static HttpRequestMessage AdminMessage(
        HttpMethod method,
        string uri,
        object? body = null)
    {
        HttpRequestMessage request = new(method, uri);
        request.Headers.Add("X-Test-Role", "HQ_ADMIN");
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    private static long ReadCheckoutId(string checkoutUrl)
        => long.Parse(checkoutUrl.Split('/', StringSplitOptions.RemoveEmptyEntries).Last());

    private static DateOnly ReadDateOnly(JsonElement element)
        => DateOnly.Parse(element.GetString()!);

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }

    private static async Task AssertStatusAsync(
        HttpStatusCode expected,
        HttpResponseMessage response)
    {
        if (response.StatusCode == expected) return;
        Assert.Fail(
            $"Expected {(int)expected} {expected}, got {(int)response.StatusCode} " +
            $"{response.StatusCode}. Body: {await response.Content.ReadAsStringAsync()}");
    }

    private static string NewSuffix()
        => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private sealed record TestStudent(long PersonId, long UserAccountId);
    private sealed record TestCourse(long CourseId, IReadOnlyDictionary<string, long> PlanIds);
    private sealed record StatementInfo(long StatementId, long EnrollmentId);
}
