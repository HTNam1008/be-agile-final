namespace Moe.Modules.FasPayment.Contracts.Payments;

public sealed record CancelEnrollmentRequest(string IdempotencyKey);

