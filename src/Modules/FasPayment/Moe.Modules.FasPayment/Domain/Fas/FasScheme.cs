using Moe.SharedKernel.Domain;

namespace Moe.Modules.FasPayment.Domain.Fas;

internal sealed class FasScheme : Entity<long>
{
    private FasScheme() : base(0) { }

    public string SchemeCode { get; private set; } = string.Empty;
    public string SchemeName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string ProviderName { get; private set; } = string.Empty;
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public DateOnly? ApplicationOpenFrom { get; private set; }
    public DateOnly? ApplicationOpenTo { get; private set; }
    public string SchemeStatusCode { get; private set; } = string.Empty;
}
