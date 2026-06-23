using Moe.SharedKernel.Domain;

namespace Moe.Modules.IdentityPlatform.Domain.People;

public sealed class Person : AggregateRoot<long>
{
    private Person() : base(0) { }

    public Person(long id, string externalReference, string fullName, DateOnly dateOfBirth, string nationalityCode, string citizenshipStatusCode) : base(id)
    {
        ExternalPersonReference = externalReference;
        OfficialFullName = fullName;
        DateOfBirth = dateOfBirth;
        NationalityCode = nationalityCode;
        CitizenshipStatusCode = citizenshipStatusCode;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public static Person CreateStudent(
        string externalReference,
        string identityNumberMasked,
        string fullName,
        DateOnly dateOfBirth,
        string nationalityCode,
        string citizenshipStatusCode,
        string? email,
        string? mobile,
        string? address,
        DateTime utcNow)
    {
        Person person = new(
            0,
            externalReference,
            fullName.Trim(),
            dateOfBirth,
            nationalityCode.Trim().ToUpperInvariant(),
            citizenshipStatusCode.Trim().ToUpperInvariant());

        person.IdentityNumberMasked = identityNumberMasked.Trim().ToUpperInvariant();
        person.OfficialEmail = NormalizeNullable(email);
        person.PreferredEmail = NormalizeNullable(email);
        person.OfficialMobile = NormalizeNullable(mobile);
        person.PreferredMobile = NormalizeNullable(mobile);
        person.OfficialAddress = NormalizeNullable(address);
        person.PreferredAddress = NormalizeNullable(address);
        person.PersonStatusCode = PersonStatusCodes.Active;
        person.SourceUpdatedAtUtc = utcNow;
        person.CreatedAtUtc = utcNow;
        person.UpdatedAtUtc = utcNow;
        return person;
    }

    public string ExternalPersonReference { get; private set; } = string.Empty;
    public string? IdentityNumberMasked { get; private set; }
    public string OfficialFullName { get; private set; } = string.Empty;
    public DateOnly DateOfBirth { get; private set; }
    public string NationalityCode { get; private set; } = string.Empty;
    public string CitizenshipStatusCode { get; private set; } = string.Empty;
    public string? OfficialEmail { get; private set; }
    public string? PreferredEmail { get; private set; }
    public string? OfficialMobile { get; private set; }
    public string? PreferredMobile { get; private set; }
    public string? OfficialAddress { get; private set; }
    public string? PreferredAddress { get; private set; }
    public string PersonStatusCode { get; private set; } = PersonStatusCodes.Active;
    public DateTime? SourceUpdatedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void UpdatePreferredContact(
        string? preferredEmail,
        string? preferredMobile,
        string? preferredAddress,
        DateTime utcNow)
    {
        PreferredEmail = NormalizeNullable(preferredEmail);
        PreferredMobile = NormalizeNullable(preferredMobile);
        PreferredAddress = NormalizeNullable(preferredAddress);
        UpdatedAtUtc = utcNow;
    }

    public void Disable(DateTime utcNow)
    {
        PersonStatusCode = PersonStatusCodes.Disabled;
        UpdatedAtUtc = utcNow;
    }

    private static string? NormalizeNullable(string? value)
    {
        string? trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}

internal static class PersonStatusCodes
{
    public const string Active = "ACTIVE";
    public const string Disabled = "DISABLED";
}
