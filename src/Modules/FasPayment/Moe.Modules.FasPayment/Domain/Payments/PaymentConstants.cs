namespace Moe.Modules.FasPayment.Domain.Payments;

internal static class PaymentMethodCodes
{
    public const string EducationAccount = "EDUCATION_ACCOUNT";
    public const string OnlinePayment = "ONLINE_PAYMENT";
    public const string OnlineTender = "ONLINE_TENDER";
    public const string Card = "CARD";
}

internal static class PaymentModes
{
    public const string AutoEducationAccountThenOnline = "AUTO_EDUCATION_ACCOUNT_THEN_ONLINE";
}

internal static class FasRoles
{
    public const string HqAdmin = "HQ_ADMIN";
    public const string SchoolAdmin = "SCHOOL_ADMIN";
    public const string Student = "STUDENT";
}
