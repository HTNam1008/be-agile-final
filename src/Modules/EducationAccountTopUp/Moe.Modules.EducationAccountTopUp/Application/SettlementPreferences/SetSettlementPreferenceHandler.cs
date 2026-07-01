using System.Text.Json;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.SettlementPreferences;

internal sealed class SetSettlementPreferenceHandler(
    IEducationAccountRepository educationAccounts,
    ISettlementPreferenceRepository settlementPreferences,
    IClock clock,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SetSettlementPreferenceCommand, SettlementPreferenceResponse>
{
    private const string CpfToken = "CPF_DEFAULT";
    private const string CpfMasked = "CPF account (linked to NRIC)";
    private static readonly JsonSerializerOptions TokenJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<SettlementPreferenceResponse>> Handle(
        SetSettlementPreferenceCommand command,
        CancellationToken cancellationToken)
    {
        EducationAccount? account = await educationAccounts.FindByPersonIdAsync(command.PersonId, cancellationToken);
        if (account is null)
        {
            return Result<SettlementPreferenceResponse>.Success(SettlementPreferenceResponse.NotApplicable());
        }

        SettlementPreference? current = await settlementPreferences.FindActiveByEducationAccountIdAsync(
            account.Id,
            cancellationToken);

        if (current is not null && command.ExpectedUpdatedAtUtc != current.UpdatedAtUtc)
        {
            return Result<SettlementPreferenceResponse>.Failure(EducationAccountErrors.SettlementPreferenceConflict);
        }

        DateTime utcNow = clock.UtcNow.UtcDateTime;
        Result<DestinationDetails> detailsResult = CreateDestinationDetails(command);
        if (detailsResult.IsFailure)
        {
            return Result<SettlementPreferenceResponse>.Failure(detailsResult.Error);
        }

        current?.Deactivate(utcNow);

        DestinationDetails details = detailsResult.Value;
        SettlementPreference preference = SettlementPreference.Create(
            account.Id,
            details.DestinationTypeCode,
            details.DestinationToken,
            details.DestinationMasked,
            details.IsVerified,
            utcNow);

        await settlementPreferences.AddAsync(preference, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<SettlementPreferenceResponse>.Success(
            SettlementPreferenceResponse.Applicable(GetSettlementPreferenceHandler.ToDto(preference)));
    }

    private static Result<DestinationDetails> CreateDestinationDetails(SetSettlementPreferenceCommand command)
    {
        string destinationTypeCode = command.DestinationTypeCode?.Trim().ToUpperInvariant() ?? string.Empty;
        return destinationTypeCode switch
        {
            SettlementDestinationTypeCodes.Cpf => Result<DestinationDetails>.Success(
                new DestinationDetails(
                    SettlementDestinationTypeCodes.Cpf,
                    CpfToken,
                    CpfMasked,
                    IsVerified: true)),
            SettlementDestinationTypeCodes.Bank => CreateBankDestinationDetails(command),
            _ => Result<DestinationDetails>.Failure(EducationAccountErrors.InvalidSettlementPreference)
        };
    }

    private static Result<DestinationDetails> CreateBankDestinationDetails(SetSettlementPreferenceCommand command)
    {
        string bankName = command.BankName?.Trim() ?? string.Empty;
        string accountNumber = command.BankAccountNumber?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(bankName)
            || bankName.Length > 120
            || accountNumber.Length is < 6 or > 34
            || accountNumber.Any(character => !char.IsDigit(character)))
        {
            return Result<DestinationDetails>.Failure(EducationAccountErrors.InvalidSettlementPreference);
        }

        string token = JsonSerializer.Serialize(new BankDestinationToken(bankName, accountNumber), TokenJsonOptions);
        string lastFour = accountNumber[^4..];

        return Result<DestinationDetails>.Success(
            new DestinationDetails(
                SettlementDestinationTypeCodes.Bank,
                token,
                $"{bankName} account ending {lastFour}",
                IsVerified: false));
    }

    private sealed record DestinationDetails(
        string DestinationTypeCode,
        string DestinationToken,
        string DestinationMasked,
        bool IsVerified);

    private sealed record BankDestinationToken(string BankName, string AccountNumber);
}
