using Moe.Application.Abstractions.Security;

namespace Moe.Modules.FasPayment.Application;

internal static class CurrentUserExtensions
{
    public static bool TryGetStudent(this ICurrentUser currentUser, out long personId)
    {
        personId = currentUser.PersonId ?? 0;
        return currentUser.IsAuthenticated
            && currentUser.Portal == "ESERVICE"
            && currentUser.Roles.Contains("STUDENT")
            && personId > 0;
    }
}
