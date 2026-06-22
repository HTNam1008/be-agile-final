using System;
using System.Collections.Generic;
using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.FasPayment.Application.Applications.GetApplicationDetail;

public sealed record GetApplicationDetailQuery(long ApplicationId) : IQuery<GetApplicationDetailResponse>;

public sealed record GetApplicationDetailResponse(
    long ApplicationId,
    string ApplicationNo,
    string StudentId,
    string StudentName,
    string SubmittedDate,
    string Status,
    ApplicationDetailScheme Scheme,
    List<ApplicationDetailDecision> DecisionHistory);

public sealed record ApplicationDetailScheme(
    long SchemeId,
    string Name);

public sealed record ApplicationDetailDecision(
    string Decision,
    string ReviewerUserId,
    DateTime ReviewedAt,
    string? Remarks);
