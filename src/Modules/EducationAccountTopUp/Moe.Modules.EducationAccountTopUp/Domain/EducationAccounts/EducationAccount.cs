using Moe.SharedKernel.Domain;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;

public sealed class EducationAccount : AggregateRoot<long>
{
    private EducationAccount() : base(0) { }

    private EducationAccount(
        long id,
        long personId,
        string accountNumber,
        string currencyCode,
        DateTimeOffset openedAtUtc,
        string openingModeCode,
        string openingReasonCode,
        string? openingRemarks,
        long? openedByUserId) : base(id)
    {
        PersonId = personId;
        AccountNumber = accountNumber;
        StatusCode = AccountStatuses.Active;
        OpenedAtUtc = openedAtUtc;
        OpeningModeCode = openingModeCode;
        OpeningReasonCode = openingReasonCode;
        OpeningRemarks = openingRemarks;
        OpenedByUserId = openedByUserId;
    }

    public long PersonId { get; private set; }
    public string AccountNumber { get; private set; } = string.Empty;
    public string StatusCode { get; private set; } = AccountStatuses.Pending;
    public DateTimeOffset OpenedAtUtc { get; private set; }
    public string OpeningModeCode { get; private set; } = string.Empty;
    public string OpeningReasonCode { get; private set; } = string.Empty;
    public string? OpeningRemarks { get; private set; }
    public long? OpenedByUserId { get; private set; }
    public DateTimeOffset? PendingClosureAtUtc { get; private set; }
    public DateOnly? ClosureExceptionUntil { get; private set; }
    public string? ClosureExceptionReason { get; private set; }
    public long? ClosureExceptionApprovedByLoginAccountId { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public string? ClosingTypeCode { get; private set; }
    public string? ClosingReasonCode { get; private set; }
    public string? ClosingRemarks { get; private set; }
    public long? ClosedByLoginAccountId { get; private set; }
    public decimal CachedBalance { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<EducationAccount> OpenManual(
        long personId,
        string accountNumber,
        DateTimeOffset now,
        string reason,
        string remarks,
        long openedBy)
    {
        if (personId <= 0)
        {
            return Result<EducationAccount>.Failure(AccountErrors.InvalidPerson);
        }

        if (string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(remarks))
        {
            return Result<EducationAccount>.Failure(AccountErrors.ManualReasonRequired);
        }

        EducationAccount account = new(
            0,
            personId,
            accountNumber.Trim(),
            CurrencyCodes.SingaporeDollar,
            now,
            AccountOpeningModeCodes.Manual,
            reason.Trim(),
            remarks.Trim(),
            openedBy);

        return Result<EducationAccount>.Success(account);
    }

    public static Result<EducationAccount> OpenAutomatically(
        long personId,
        string accountNumber,
        DateTimeOffset now)
    {
        if (personId <= 0)
        {
            return Result<EducationAccount>.Failure(AccountErrors.InvalidPerson);
        }

        EducationAccount account = new(
            0,
            personId,
            accountNumber.Trim(),
            CurrencyCodes.SingaporeDollar,
            now,
            AccountOpeningModeCodes.Automatic,
            EducationAccountOpeningReasonCodes.AutoEligibility,
            null,
            null);

        return Result<EducationAccount>.Success(account);
    }

    public Result CloseManual(DateTimeOffset now, string reasonCode, string? remarks, long closedByLoginAccountId)
    {
        if (StatusCode == AccountStatuses.Closed)
        {
            return Result.Failure(AccountErrors.AlreadyClosed);
        }

        if (string.IsNullOrWhiteSpace(reasonCode))
        {
            return Result.Failure(AccountErrors.ManualReasonRequired);
        }

        StatusCode = AccountStatuses.Closed;
        ClosedAtUtc = now;
        ClosingReasonCode = reasonCode.Trim();
        ClosingRemarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
        ClosedByLoginAccountId = closedByLoginAccountId;
        return Result.Success();
    }

    public Result<bool> CloseAutomatically(DateTimeOffset now)
    {
        if (StatusCode == AccountStatuses.Closed)
        {
            return Result<bool>.Success(false);
        }

        StatusCode = AccountStatuses.Closed;
        ClosedAtUtc = now;
        ClosingReasonCode = EducationAccountClosingReasonCodes.AutoAgeLimit;
        ClosingRemarks = null;
        ClosedByLoginAccountId = null;
        return Result<bool>.Success(true);
    }

    public void UpdateBalance(decimal amount)
    {
        CachedBalance += amount;
    }
}

public static class CurrencyCodes
{
    public const string SingaporeDollar = "SGD";
}

public static class AccountOpeningModeCodes
{
    public const string Manual = "MANUAL";
    public const string Automatic = "AUTOMATIC";
}

public static class AccountStatuses
{
    public const string Pending = "PENDING";
    public const string Active = "ACTIVE";
    public const string Closing = "CLOSING";
    public const string Closed = "CLOSED";
}

public static class AccountErrors
{
    public static readonly Error InvalidPerson = new("ACCOUNT.INVALID_PERSON", "A valid person is required.");
    public static readonly Error ManualReasonRequired = new("ACCOUNT.MANUAL_REASON_REQUIRED", "Manual actions require a reason and remarks.");
    public static readonly Error AlreadyClosed = new("ACCOUNT.ALREADY_CLOSED", "The Education Account is already closed.");
    public static readonly Error DuplicatePersonAccount = new("ACCOUNT.DUPLICATE", "The person already has an Education Account.");
    public static readonly Error OrganizationOutsideScope = new("AUTH.ORGANIZATION_OUTSIDE_SCOPE", "The requested organization is outside the current admin's scope.");
    public static readonly Error ActorRequired = new("ACCOUNT.ACTOR_REQUIRED", "A closing admin is required.");
}
