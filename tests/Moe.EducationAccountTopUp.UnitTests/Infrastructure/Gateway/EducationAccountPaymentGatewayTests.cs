using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.Accounts;
using Moe.Modules.EducationAccountTopUp.Infrastructure.Gateway;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.Infrastructure.Gateway;

public sealed class EducationAccountPaymentGatewayTests
{
    [Fact]
    public async Task CaptureAsync_WhenAccountIsClosed_FailsWithoutMutatingBalanceOrHold()
    {
        await using MoeDbContext dbContext = CreateDbContext();
        DateTimeOffset now = new(2026, 6, 30, 4, 0, 0, TimeSpan.Zero);
        EducationAccount account = EducationAccount.OpenAutomatically(
            personId: 7001,
            accountNumber: "PSEA-00007001",
            now: now.AddYears(-1)).Value;
        account.UpdateBalance(50m);
        account.CloseAutomatically(now).IsSuccess.Should().BeTrue();
        dbContext.Set<EducationAccount>().Add(account);
        await dbContext.SaveChangesAsync();
        AccountHold hold = AccountHold.Reserve(
            educationAccountId: account.Id,
            paymentPartId: 9001,
            amount: 25m,
            createdAtUtc: now.UtcDateTime.AddMinutes(-1),
            expiresAtUtc: now.UtcDateTime.AddMinutes(29));
        dbContext.Set<AccountHold>().Add(hold);
        await dbContext.SaveChangesAsync();
        EducationAccountPaymentGateway gateway = new(dbContext);

        Func<Task> act = () => gateway.CaptureAsync(hold.Id, actorLoginAccountId: null, CancellationToken.None);

        await act.Should().ThrowAsync<EducationAccountPaymentUnavailableException>();
        EducationAccount reloadedAccount = await dbContext.Set<EducationAccount>().SingleAsync(x => x.Id == account.Id);
        AccountHold reloadedHold = await dbContext.Set<AccountHold>().SingleAsync(x => x.Id == hold.Id);
        reloadedAccount.CachedBalance.Should().Be(50m);
        reloadedHold.HoldStatusCode.Should().Be(AccountHoldStatusCodes.Reserved);
        reloadedHold.AccountTransactionId.Should().BeNull();
        dbContext.Set<AccountTransaction>().Should().BeEmpty();
    }

    private static MoeDbContext CreateDbContext()
    {
        DbContextOptions<MoeDbContext> options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MoeDbContext(options, [new EducationAccountTopUpModelConfiguration()]);
    }
}
