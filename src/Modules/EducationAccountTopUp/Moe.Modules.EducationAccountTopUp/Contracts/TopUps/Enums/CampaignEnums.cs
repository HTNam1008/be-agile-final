namespace Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;

public enum TopUpCampaignStatusCode
{
    Draft,
    Active,
    Paused,
    Completed,
    Cancelled
}

public enum TopUpRunStatusCode
{
    Previewed,
    Processing,
    Completed,
    Partial,
    Failed,
    Cancelled
}

public enum TopUpTransactionStatusCode
{
    Pending,
    Completed,
    Failed,
    Skipped
}

public enum RecipientModeCode
{
    FixedSelection,
    DynamicRules
}

public enum ScheduleTypeCode
{
    Immediate,
    OneTimeScheduled,
    Recurring
}

public enum FrequencyCode
{
    Daily,
    Weekly,
    Monthly,
    Quarterly,
    Yearly
}

public enum TopUpCriterionCode
{
    Age,
    AccountBalance,
    SchoolingStatus,
    Level,
    Class
}

public enum OperatorCode
{
    Equals,
    NotEquals,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Between,
    In
}

public enum TopUpTriggerTypeCode
{
    Manual,
    Scheduled
}
