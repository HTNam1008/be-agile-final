namespace Moe.Modules.CourseBilling.Contracts.AdminFeeComponents;

public sealed record FeeComponentQueryRequest(
    string? Keyword,
    bool? IsActive,
    int Page = 1,
    int PageSize = 10);

public sealed record CreateFeeComponentRequest(
    string ComponentCode,
    string ComponentName,
    string ComponentTypeCode,
    string CalculationTypeCode,
    bool IsTaxComponent,
    decimal DefaultValue = 0m,
    bool IsActive = true);

public sealed record UpdateFeeComponentRequest(
    string ComponentCode,
    string ComponentName,
    string ComponentTypeCode,
    string CalculationTypeCode,
    bool IsTaxComponent,
    decimal DefaultValue,
    bool IsActive);

public sealed record FeeComponentDto(
    long FeeComponentId,
    string ComponentCode,
    string ComponentName,
    string ComponentTypeCode,
    string CalculationTypeCode,
    bool IsTaxComponent,
    decimal DefaultValue,
    bool IsSystemManaged,
    bool IsActive);
