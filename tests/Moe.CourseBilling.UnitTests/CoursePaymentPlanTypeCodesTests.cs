using Moe.Modules.CourseBilling.IGateway.Payments;
using Xunit;

namespace Moe.CourseBilling.UnitTests;

public sealed class CoursePaymentPlanTypeCodesTests
{
    [Fact]
    public void Installment_ShouldExposeCanonicalPaymentPlanTypeCode()
    {
        Assert.Equal("INSTALLMENT", CoursePaymentPlanTypeCodes.Installment);
    }
}
