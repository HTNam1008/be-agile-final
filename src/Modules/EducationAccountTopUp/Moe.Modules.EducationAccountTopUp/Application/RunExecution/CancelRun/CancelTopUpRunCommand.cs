using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution.CancelRun;

public sealed record CancelTopUpRunCommand(long RunId) : ICommand;
