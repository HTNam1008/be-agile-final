namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

public sealed record CloseManualAccountRequest(
    string ReasonCode,
    string? Remarks);
