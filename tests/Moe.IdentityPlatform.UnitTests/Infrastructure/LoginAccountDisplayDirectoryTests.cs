using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Infrastructure.Authentication;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.IdentityPlatform.UnitTests.Infrastructure;

public sealed class LoginAccountDisplayDirectoryTests : IAsyncLifetime
{
    private readonly MoeDbContext _dbContext;
    private readonly LoginAccountDisplayDirectory _directory;

    public LoginAccountDisplayDirectoryTests()
    {
        DbContextOptions<MoeDbContext> options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase($"login-account-display-{Guid.NewGuid():N}")
            .Options;

        _dbContext = new MoeDbContext(
            options,
            [new LoginAccountDisplayTestModelConfiguration()]);

        _directory = new LoginAccountDisplayDirectory(_dbContext);
    }

    public async Task InitializeAsync()
    {
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task FindDisplayNamesAsync_ReturnsDisplayNamesForRequestedAccounts()
    {
        UserAccount admin = CreateAdmin(id: 42, displayName: "Admin One", email: "admin.one@moe.local");
        UserAccount fallbackAdmin = CreateAdmin(id: 43, displayName: null, email: "fallback@moe.local");
        UserAccount unrequestedAdmin = CreateAdmin(id: 44, displayName: "Not Requested", email: "not.requested@moe.local");
        _dbContext.AddRange(admin, fallbackAdmin, unrequestedAdmin);
        await _dbContext.SaveChangesAsync();

        IReadOnlyDictionary<long, string> names = await _directory.FindDisplayNamesAsync(
            [42, 43, 999],
            CancellationToken.None);

        names.Should().BeEquivalentTo(new Dictionary<long, string>
        {
            [42] = "Admin One",
            [43] = "FALLBACK@MOE.LOCAL"
        });
    }

    private static UserAccount CreateAdmin(long id, string? displayName, string email)
    {
        UserAccount account = UserAccount.CreateAdmin(
            externalIssuer: "issuer",
            externalSubjectId: $"subject-{id}",
            externalTenantId: "tenant",
            externalObjectId: $"object-{id}",
            email,
            displayName,
            RoleCodes.SchoolAdmin,
            adminOrganizationId: 10,
            createdByUserAccountId: 1,
            utcNow: new DateTime(2026, 6, 22, 8, 0, 0, DateTimeKind.Utc));

        typeof(UserAccount).GetProperty(nameof(UserAccount.Id))!.SetValue(account, id);
        return account;
    }

    private sealed class LoginAccountDisplayTestModelConfiguration : IModelConfigurationContributor
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserAccount>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
                builder.Ignore(x => x.DomainEvents);
            });
        }
    }
}
