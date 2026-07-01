using Moe.Modules.CourseBilling.IGateway.Storage;

namespace Moe.Modules.CourseBilling.Infrastructure.Storage;

internal interface ICourseMaterialPreviewRedisCache : ICourseMaterialPreviewCache;

internal interface ICourseMaterialPreviewBlobCache : ICourseMaterialPreviewCache;
