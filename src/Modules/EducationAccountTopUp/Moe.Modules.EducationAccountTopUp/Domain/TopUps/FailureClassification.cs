namespace Moe.Modules.EducationAccountTopUp.Domain.TopUps;

public enum FailureKind
{
    Transient,
    Permanent
}

public static class FailureClassifier
{
    public static FailureKind Classify(Exception exception)
    {
        string exceptionTypeName = exception.GetType().Name;

        if (string.Equals(exceptionTypeName, "DbUpdateConcurrencyException", StringComparison.Ordinal)
            || exception is TimeoutException
            || exception is TaskCanceledException
            || exception.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return FailureKind.Transient;
        }

        return FailureKind.Permanent;
    }

    public static FailureKind Classify(string errorCodeOrReason)
    {
        return errorCodeOrReason switch
        {
            "TopUp.CreditServiceUnavailable" => FailureKind.Transient,
            SafeReasons.CreditServiceUnavailable => FailureKind.Transient,
            "Credit service unavailable" => FailureKind.Transient,
            _ => FailureKind.Permanent
        };
    }
}
