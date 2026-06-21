namespace Moe.Modules.EducationAccountTopUp.IGateway.TopUps;

public sealed record ActiveRecipientProjection(
    long EducationAccountId,
    decimal? AmountOverride);
