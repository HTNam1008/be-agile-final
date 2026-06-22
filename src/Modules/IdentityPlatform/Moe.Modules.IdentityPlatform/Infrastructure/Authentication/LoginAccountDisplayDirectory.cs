using Microsoft.EntityFrameworkCore;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.IGateway.Accounts;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Authentication;

internal sealed class LoginAccountDisplayDirectory(MoeDbContext dbContext) : ILoginAccountDisplayDirectory
{
    public async Task<IReadOnlyDictionary<long, string>> FindDisplayNamesAsync(
        IReadOnlyCollection<long> loginAccountIds,
        CancellationToken cancellationToken)
    {
        if (loginAccountIds.Count == 0)
        {
            return new Dictionary<long, string>();
        }

        long[] ids = loginAccountIds.Distinct().ToArray();

        var rows = await dbContext.Set<UserAccount>()
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.DisplayNameSnapshot,
                x.ProviderDisplayName,
                x.ProviderLoginName,
                x.ContactEmail,
                x.LoginEmailNormalized
            })
            .ToArrayAsync(cancellationToken);

        return rows
            .Select(x => new
            {
                x.Id,
                DisplayName = FirstNonBlank(
                    x.DisplayNameSnapshot,
                    x.ProviderDisplayName,
                    x.ProviderLoginName,
                    x.ContactEmail,
                    x.LoginEmailNormalized)
            })
            .Where(x => x.DisplayName is not null)
            .ToDictionary(x => x.Id, x => x.DisplayName!);
    }

    private static string? FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim();
}
