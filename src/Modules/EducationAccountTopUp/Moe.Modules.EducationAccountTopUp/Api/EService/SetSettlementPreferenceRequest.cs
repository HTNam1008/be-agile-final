namespace Moe.Modules.EducationAccountTopUp.Api.EService;

public sealed record SetSettlementPreferenceRequest(
    string DestinationTypeCode,
    string? BankName,
    string? BankAccountNumber,
    DateTime? ExpectedUpdatedAtUtc);
