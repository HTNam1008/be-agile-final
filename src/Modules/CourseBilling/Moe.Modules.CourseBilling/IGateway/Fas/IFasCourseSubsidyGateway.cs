namespace Moe.Modules.CourseBilling.IGateway.Fas;

internal interface IFasCourseSubsidyGateway
{
    Task<IReadOnlyCollection<CourseFasSubsidy>> ListEligibleSubsidiesAsync(
        long personId,
        long courseId,
        DateOnly enrolledDate,
        long? courseEnrollmentId,
        IReadOnlyCollection<long>? fasApplicationSchemeIds,
        CancellationToken cancellationToken);

    Task RecordPendingRedemptionsAsync(
        long personId,
        long courseId,
        long courseEnrollmentId,
        long billId,
        decimal totalSubsidyAmount,
        IReadOnlyCollection<CourseFasSubsidy> subsidies,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task RedeemPendingRedemptionsForBillsAsync(
        IReadOnlyCollection<long> billIds,
        DateTime redeemedAtUtc,
        CancellationToken cancellationToken);

    Task CancelPendingRedemptionsForEnrollmentAsync(
        long courseEnrollmentId,
        DateTime cancelledAtUtc,
        CancellationToken cancellationToken);
}

internal sealed record CourseFasSubsidy(
    long FasApplicationSchemeId,
    string SubsidyTypeCode,
    decimal SubsidyValue);
