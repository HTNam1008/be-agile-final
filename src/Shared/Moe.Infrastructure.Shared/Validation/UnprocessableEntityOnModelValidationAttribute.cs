namespace Moe.Infrastructure.Shared.Validation;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class UnprocessableEntityOnModelValidationAttribute : Attribute;
