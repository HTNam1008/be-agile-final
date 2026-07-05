using FluentAssertions;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.FasPayment.Application.PaymentPlans;
using Xunit;

namespace Moe.FasPayment.UnitTests;

public sealed class CoursePaymentPlanPolicyHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnPaymentPlanPolicyResponse()
    {
        var handler = new GetCoursePaymentPlanPolicyHandler();

        var result = await handler.Handle(new GetCoursePaymentPlanPolicyQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PlanTypeCodes.FullPayment.Should().Be(CoursePaymentPlanTypeCodes.FullPayment);
        result.Value.PlanTypeCodes.Installment.Should().Be(CoursePaymentPlanTypeCodes.Installment);
        result.Value.AllowedInstallmentCounts.Should().Equal(2, 3, 6, 9, 12);
        result.Value.DefaultInstallmentCount.Should().Be(3);
    }
}
