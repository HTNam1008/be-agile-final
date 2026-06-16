namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

public sealed record OpenManualAccountRequest(
    long PersonId,
    string ReasonCode,
    string Remarks);
