using Moe.Application.Abstractions.Messaging;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.SettlementPreferences;

internal sealed class GetSettlementPreferenceHandler(
    IEducationAccountRepository educationAccounts,
    ISettlementPreferenceRepository settlementPreferences)
    : IQueryHandler<GetSettlementPreferenceQuery, SettlementPreferenceResponse>
{
    public async Task<Result<SettlementPreferenceResponse>> Handle(
        GetSettlementPreferenceQuery query,
        CancellationToken cancellationToken)
    {
        EducationAccount? account = await educationAccounts.FindByPersonIdAsync(query.PersonId, cancellationToken);
        if (account is null)
        {
            return Result<SettlementPreferenceResponse>.Success(SettlementPreferenceResponse.NotApplicable());
        }

        SettlementPreference? preference = await settlementPreferences.FindActiveByEducationAccountIdAsync(
            account.Id,
            cancellationToken);

        return Result<SettlementPreferenceResponse>.Success(
            SettlementPreferenceResponse.Applicable(preference is null ? null : ToDto(preference)));
    }

    internal static SettlementPreferenceDto ToDto(SettlementPreference preference)
        => new(
            preference.DestinationTypeCode,
            preference.DestinationMasked,
            preference.IsVerified,
            preference.UpdatedAtUtc);
}
