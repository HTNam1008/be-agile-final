using System.Collections.Generic;
using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.FasPayment.Application.Applications.GetSchemeApplications;

public sealed record GetSchemeApplicationsQuery(long SchemeId) : IQuery<GetSchemeApplicationsResponse>;

public sealed record GetSchemeApplicationsResponse(
    SchemeApplicationsSummary Summary,
    List<SchemeApplicationItem> Items);

public sealed record SchemeApplicationsSummary(
    int PendingReview,
    int Approved,
    int Rejected);

public sealed record SchemeApplicationItem(
    long ApplicationId,
    string ApplicationNo,
    string StudentName,
    string StudentId,
    string SubmittedDate,
    string Status);
