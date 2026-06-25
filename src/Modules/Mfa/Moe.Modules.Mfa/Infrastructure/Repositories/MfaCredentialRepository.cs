using Microsoft.EntityFrameworkCore;
using Moe.Modules.Mfa.Domain;
using Moe.Modules.Mfa.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.Mfa.Infrastructure.Repositories;

internal sealed class MfaCredentialRepository(MoeDbContext dbContext) : IMfaCredentialRepository
{
    public Task<bool> HasActivePinAsync(long loginAccountId, CancellationToken cancellationToken)
    {
        return dbContext.Set<LoginMfaCredential>()
            .AnyAsync(x => x.LoginAccountId == loginAccountId
                && x.MfaTypeCode == MfaTypeCodes.Pin
                && x.StatusCode == MfaCredentialStatusCodes.Active,
                cancellationToken);
    }

    public Task<LoginMfaCredential?> FindPinAsync(long loginAccountId, CancellationToken cancellationToken)
    {
        return dbContext.Set<LoginMfaCredential>()
            .SingleOrDefaultAsync(x => x.LoginAccountId == loginAccountId
                && x.MfaTypeCode == MfaTypeCodes.Pin,
                cancellationToken);
    }

    public Task<LoginMfaCredential?> FindActivePinAsync(long loginAccountId, CancellationToken cancellationToken)
    {
        return dbContext.Set<LoginMfaCredential>()
            .SingleOrDefaultAsync(x => x.LoginAccountId == loginAccountId
                && x.MfaTypeCode == MfaTypeCodes.Pin
                && x.StatusCode == MfaCredentialStatusCodes.Active,
                cancellationToken);
    }

    public void Add(LoginMfaCredential credential)
    {
        dbContext.Add(credential);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
