namespace Moe.Modules.FasPayment.IGateway.Repositories;

internal enum FasSchemeUniqueField
{
    SchemeCode,
    GrantCode,
    Unknown
}

internal sealed class FasSchemeWriteConflictException(FasSchemeUniqueField field, Exception innerException)
    : Exception("A unique FAS scheme value was inserted concurrently.", innerException)
{
    public FasSchemeUniqueField Field { get; } = field;
}
