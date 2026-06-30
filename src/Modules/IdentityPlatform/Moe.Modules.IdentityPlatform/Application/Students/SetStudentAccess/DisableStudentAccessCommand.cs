using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.IdentityPlatform.Application.Students.SetStudentAccess;

public sealed record DisableStudentAccessCommand(long PersonId) : ICommand<StudentAccessResponse>;
