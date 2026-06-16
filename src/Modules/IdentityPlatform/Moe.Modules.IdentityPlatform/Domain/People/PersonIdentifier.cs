using Moe.SharedKernel.Domain;

namespace Moe.Modules.IdentityPlatform.Domain.People;

internal sealed class PersonIdentifier : AggregateRoot<long>
{
    private PersonIdentifier() : base(0) { }

    public PersonIdentifier(
        long personId,
        string identifierTypeCode,
        byte[] identifierValueHash,
        string? identifierMasked,
        string sourceSystemCode,
        DateTime createdAtUtc) : base(0)
    {
        PersonId = personId;
        IdentifierTypeCode = identifierTypeCode;
        IdentifierValueHash = identifierValueHash;
        IdentifierMasked = identifierMasked;
        SourceSystemCode = sourceSystemCode;
        IdentifierStatusCode = PersonIdentifierStatusCodes.Active;
        IsPrimary = true;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public long PersonId { get; private set; }
    public string IdentifierTypeCode { get; private set; } = string.Empty;
    public byte[]? IdentifierValueEncrypted { get; private set; }
    public byte[] IdentifierValueHash { get; private set; } = [];
    public string? IdentifierMasked { get; private set; }
    public string? IssuingCountryCode { get; private set; }
    public string? IssuedByAuthority { get; private set; }
    public string IdentifierStatusCode { get; private set; } = string.Empty;
    public bool IsPrimary { get; private set; }
    public DateOnly? EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public string SourceSystemCode { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}

internal static class PersonIdentifierStatusCodes
{
    public const string Active = "ACTIVE";
}
