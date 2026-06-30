using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.IdentityPlatform.Application.Students.SetStudentAccess;

public sealed record EnableStudentAccessCommand(long PersonId) : ICommand<StudentAccessResponse>;
