using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.IdentityPlatform.Application.StudentProfile.GetMyStudentProfile;

public sealed record GetMyStudentProfileQuery : IQuery<StudentProfileResponse>;
