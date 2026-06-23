using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.CourseBilling.Application.Dashboard.GetAdminDashboard;

public sealed record GetAdminDashboardQuery(
    int? Year,
    long? OrganizationId) : IQuery<AdminDashboardResponse>;
