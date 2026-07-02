using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Infrastructure.Students;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.IdentityPlatform.UnitTests.Infrastructure.Students;

public sealed class StudentNotificationRecipientResolverTests
{
    [Fact]
    public async Task FindUserAccountIdByPersonIdAsync_WhenLegacyDuplicatesExist_ReturnsActiveAccount()
    {
        await using MoeDbContext dbContext = CreateDbContext();
        UserAccount disabled = CreateAccount(7001, "disabled-subject");
        disabled.Disable(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        UserAccount active = CreateAccount(7001, "active-subject");
        active.ActivateFirstLogin(new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc));
        dbContext.AddRange(disabled, active);
        await dbContext.SaveChangesAsync();
        StudentNotificationRecipientResolver resolver = new(dbContext);

        long? result = await resolver.FindUserAccountIdByPersonIdAsync(7001, CancellationToken.None);

        result.Should().Be(active.Id);
    }

    [Fact]
    public async Task FindUserAccountIdByPersonIdAsync_WhenAccountDoesNotExist_ReturnsNull()
    {
        await using MoeDbContext dbContext = CreateDbContext();
        StudentNotificationRecipientResolver resolver = new(dbContext);

        long? result = await resolver.FindUserAccountIdByPersonIdAsync(9999, CancellationToken.None);

        result.Should().BeNull();
    }

    private static UserAccount CreateAccount(long personId, string subject)
        => UserAccount.CreateStudentSingpass(
            personId,
            "mockpass",
            subject,
            subject,
            null,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    private static MoeDbContext CreateDbContext()
    {
        DbContextOptions<MoeDbContext> options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase($"student-notification-recipient-{Guid.NewGuid():N}")
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
