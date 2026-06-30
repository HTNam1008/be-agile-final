using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Infrastructure.Repositories;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.IdentityPlatform.UnitTests.Infrastructure;

public sealed class UserAccountRepositoryTests
{
    [Fact]
    public async Task EnableAsync_OnDisabledAccount_SetsStatusToActive()
    {
        DateTime createdAtUtc = new(2026, 6, 30, 8, 0, 0, DateTimeKind.Utc);
        DateTime enabledAtUtc = new(2026, 6, 30, 9, 0, 0, DateTimeKind.Utc);
        await using MoeDbContext dbContext = CreateDbContext();
        UserAccount account = UserAccount.CreateStudentSingpass(
            personId: 5001,
            externalIssuer: "mockpass",
            externalSubjectId: "subject-5001",
            displayName: "Student 5001",
            createdByUserAccountId: null,
            createdAtUtc);
        account.Disable(createdAtUtc.AddMinutes(5));
        dbContext.Add(account);
        await dbContext.SaveChangesAsync();
        UserAccountRepository repository = new(dbContext);

        UserAccount? result = await repository.EnableAsync(account.Id, enabledAtUtc, CancellationToken.None);

        result.Should().NotBeNull();
        result!.AccountStatusCode.Should().Be(UserAccountStatusCodes.Active);
        result.UpdatedAtUtc.Should().Be(enabledAtUtc);
    }

    private static MoeDbContext CreateDbContext()
    {
        DbContextOptions<MoeDbContext> options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase($"user-account-repository-{Guid.NewGuid():N}")
            .Options;

        return new MoeDbContext(options, [new TestModelConfiguration()]);
    }

    private sealed class TestModelConfiguration : IModelConfigurationContributor
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserAccount>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedOnAdd();
                builder.Ignore(x => x.DomainEvents);
            });
        }
    }
}
