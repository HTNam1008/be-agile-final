namespace Moe.Modules.EducationAccountTopUp.Application.TopUps;

internal static class TopUpDisplayMasker
{
    public static string MaskAccountNumber(string accountNumber)
    {
        string trimmed = accountNumber.Trim();
        string[] parts = trimmed.Split('-', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length >= 3)
        {
            return $"{parts[0]}-****-{parts[^1]}";
        }

        if (trimmed.Length <= 4)
        {
            return "****";
        }

        return $"****{trimmed[^4..]}";
    }

    public static string MaskStudentNumber(string studentNumber)
        => MaskIdentifier(studentNumber.Trim());

    private static string MaskIdentifier(string value)
    {
        if (value.Length <= 4)
        {
            return "****";
        }

        return $"{new string('*', value.Length - 4)}{value[^4..]}";
    }
}
