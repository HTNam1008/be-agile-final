using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.IdentityPlatform.Application.StudentProfile.UpdateMyStudentContact;

public sealed record UpdateMyStudentContactCommand(
    string? ContactEmail,
    string? ContactMobile) : ICommand<StudentProfileResponse>;
