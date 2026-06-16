using Moe.SharedKernel.Domain;

namespace Moe.Modules.FasPayment.Domain.Fas;

internal sealed class CourseFasScheme : Entity<long>
{
    private CourseFasScheme() : base(0) { }

    public long CourseId { get; private set; }
    public long FasSchemeId { get; private set; }
    public string StatusCode { get; private set; } = string.Empty;
}
