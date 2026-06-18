using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution.GetRunSummary;

public sealed record GetRunSummaryQuery(long TopUpRunId) : IQuery<RunSummaryResponse>;
