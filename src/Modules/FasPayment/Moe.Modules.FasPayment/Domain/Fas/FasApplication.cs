using Moe.SharedKernel.Domain;

namespace Moe.Modules.FasPayment.Domain.Fas;

internal sealed class FasApplication : Entity<long>
{
    private FasApplication() : base(0) { }

    public string ApplicationNumber { get; private set; } = string.Empty;
    public long FasSchemeId { get; private set; }
    public long PersonId { get; private set; }
    public long? CourseId { get; private set; }
    public string ApplicationStatusCode { get; private set; } = string.Empty;
    public string? NationalitySnapshot { get; private set; }
    public decimal? HouseholdIncomeSnapshot { get; private set; }
    public int? HouseholdSizeSnapshot { get; private set; }
    public decimal? PerCapitaIncomeSnapshot { get; private set; }
    public long? SelectedTierId { get; private set; }
    public string? EvaluationResultCode { get; private set; }
    public DateTime? EvaluatedAtUtc { get; private set; }
    public DateTime? ApplicantConfirmedAtUtc { get; private set; }
    public DateTime? SubmittedAtUtc { get; private set; }
}
