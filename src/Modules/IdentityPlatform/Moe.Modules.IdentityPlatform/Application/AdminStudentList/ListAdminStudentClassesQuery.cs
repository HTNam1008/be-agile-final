using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.IdentityPlatform.Application.AdminStudentList;

internal sealed record ListAdminStudentClassesQuery(
    long OrganizationId,
    string LevelCode) : IQuery<IReadOnlyList<string>>;
