using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.EducationAccountTopUp.Application.SettlementPreferences;

public sealed record SettlementPreferenceDto(
    string DestinationTypeCode,
    string DestinationMasked,
    bool IsVerified,
    DateTime UpdatedAtUtc);

public sealed record SettlementPreferenceResponse(
    bool IsApplicable,
    string? EmptyStateMessage,
    SettlementPreferenceDto? Preference)
{
    public static SettlementPreferenceResponse NotApplicable()
        => new(false, "Available once your Education Account is opened", null);

    public static SettlementPreferenceResponse Applicable(SettlementPreferenceDto? preference)
        => new(true, null, preference);
}

public sealed record GetSettlementPreferenceQuery(long PersonId)
    : IQuery<SettlementPreferenceResponse>;

public sealed record SetSettlementPreferenceCommand(
    long PersonId,
    string DestinationTypeCode,
    string? BankName,
    string? BankAccountNumber,
    DateTime? ExpectedUpdatedAtUtc)
    : ICommand<SettlementPreferenceResponse>;
