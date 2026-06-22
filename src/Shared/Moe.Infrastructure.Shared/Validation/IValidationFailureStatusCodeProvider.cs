namespace Moe.Infrastructure.Shared.Validation;

public interface IValidationFailureStatusCodeProvider
{
    int ValidationFailureStatusCode { get; }
}
