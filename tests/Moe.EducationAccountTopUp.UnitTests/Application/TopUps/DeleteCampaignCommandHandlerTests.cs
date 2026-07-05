using System.Reflection;
using FluentAssertions;
using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.DeleteCampaign;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.Application.TopUps;

public sealed class DeleteCampaignCommandHandlerTests
{
    private readonly FakeTopUpCampaignRepository _campaigns = new();
    private readonly FakeTopUpCampaignDeletionRepository _deletion = new();
    private readonly FakeTopUpRunRepository _runs = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    [Fact]
    public async Task Handle_OnDraftCampaignWithoutRuns_DeletesCampaign()
    {
        TopUpCampaign campaign = CreateCampaign(id: 10);
        _campaigns.Add(campaign);
        DeleteCampaignCommandHandler handler = CreateHandler();

        Result result = await handler.Handle(new DeleteCampaignCommand(campaign.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _deletion.DeletedCampaignIds.Should().ContainSingle().Which.Should().Be(campaign.Id);
        _unitOfWork.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task Handle_OnActiveCampaign_ReturnsCannotDeleteNonDraft()
    {
        TopUpCampaign campaign = CreateCampaign(id: 11);
        campaign.ChangeStatus(
            TopUpCampaignStatusCodes.Active,
            currentUserId: 99,
            nowUtc: new DateTime(2026, 7, 5, 2, 0, 0, DateTimeKind.Utc));
        _campaigns.Add(campaign);
        DeleteCampaignCommandHandler handler = CreateHandler();

        Result result = await handler.Handle(new DeleteCampaignCommand(campaign.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpErrors.CannotDeleteNonDraftCampaign);
        _deletion.DeletedCampaignIds.Should().BeEmpty();
        _unitOfWork.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Handle_OnDraftCampaignWithRuns_ReturnsCannotDeleteCampaignWithRuns()
    {
        TopUpCampaign campaign = CreateCampaign(id: 12);
        _campaigns.Add(campaign);
        _runs.CampaignIdsWithRuns.Add(campaign.Id);
        DeleteCampaignCommandHandler handler = CreateHandler();

        Result result = await handler.Handle(new DeleteCampaignCommand(campaign.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpErrors.CannotDeleteCampaignWithRuns);
        _deletion.DeletedCampaignIds.Should().BeEmpty();
        _unitOfWork.SaveCalls.Should().Be(0);
    }

    private DeleteCampaignCommandHandler CreateHandler()
        => new(
            _campaigns,
            _deletion,
            _runs,
            new AllowAllAdminAccess(),
            new NoopAuditService(),
            _unitOfWork);

    private static TopUpCampaign CreateCampaign(long id)
    {
        TopUpCampaign campaign = TopUpCampaign.Create(
            organizationId: 1,
            campaignCode: $"CAMPAIGN-{id}",
            campaignName: "Test campaign",
            description: null,
            recipientModeCode: RecipientModeCode.FixedSelection.ToString(),
            defaultTopUpAmount: 100m,
            reason: "Test top-up",
            scheduleTypeCode: ScheduleTypeCode.Immediate.ToString(),
            startDate: new DateOnly(2026, 7, 5),
            endDate: null,
            frequencyCode: null,
            frequencyInterval: null,
            deliveryTypeCode: DeliveryType.Instant,
            maxTotalAmount: 100m,
            currentUserId: 99,
            nowUtc: new DateTime(2026, 7, 5, 2, 0, 0, DateTimeKind.Utc));

        typeof(TopUpCampaign)
            .BaseType!
            .GetProperty(nameof(TopUpCampaign.Id), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(campaign, id);

        return campaign;
    }

    private sealed class FakeTopUpCampaignRepository : ITopUpCampaignRepository
    {
        private readonly Dictionary<long, TopUpCampaign> _campaigns = [];

        public void Add(TopUpCampaign campaign) => _campaigns[campaign.Id] = campaign;

        public Task<TopUpCampaign?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(_campaigns.GetValueOrDefault(id));

        public Task<IReadOnlyList<TopUpCampaign>> GetByIdsAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TopUpCampaign>>([]);

        public Task<bool> CampaignCodeExistsAsync(long organizationId, string campaignCode, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<IReadOnlyList<TopUpCampaign>> GetDueCampaignsAsync(DateTime utcNow, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TopUpCampaign>>([]);

        public Task<IReadOnlyList<TopUpCampaign>> GetDueForAssessmentAsync(DateOnly today, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TopUpCampaign>>([]);

        public Task<int> CountActiveRulesAsync(long campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> CountActiveRecipientsAsync(long campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<IReadOnlyList<TopUpCampaignRecipient>> GetRecipientsAsync(long campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TopUpCampaignRecipient>>([]);

        public Task<Dictionary<long, decimal>> GetAmountOverridesByCampaignAsync(long campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult(new Dictionary<long, decimal>());

        public Task RemoveRecipientsAsync(IEnumerable<TopUpCampaignRecipient> recipients, long userId, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddRecipientAsync(TopUpCampaignRecipient recipient, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddAsync(TopUpCampaign campaign, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeTopUpCampaignDeletionRepository : ITopUpCampaignDeletionRepository
    {
        private readonly List<long> _deletedCampaignIds = [];
        public IReadOnlyCollection<long> DeletedCampaignIds => _deletedCampaignIds.AsReadOnly();

        public Task DeleteDraftAsync(TopUpCampaign campaign, CancellationToken cancellationToken = default)
        {
            _deletedCampaignIds.Add(campaign.Id);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTopUpRunRepository : ITopUpRunRepository
    {
        public HashSet<long> CampaignIdsWithRuns { get; } = [];

        public Task<TopUpRun?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult<TopUpRun?>(null);

        public Task<IReadOnlyList<TopUpRun>> GetByIdsAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TopUpRun>>([]);

        public Task<TopUpRun?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
            => Task.FromResult<TopUpRun?>(null);

        public Task<bool> ExistsForScheduledOccurrenceAsync(long campaignId, DateTime scheduledFor, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> HasRunsForCampaignAsync(long campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult(CampaignIdsWithRuns.Contains(campaignId));

        public Task<bool> HasActiveRunsForCampaignAsync(long campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task AddAsync(TopUpRun run, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.FromResult(1);
        }
    }

    private sealed class AllowAllAdminAccess : IAdminAccessControl
    {
        public bool IsHqAdmin => true;
        public bool IsSchoolAdmin => false;
        public IReadOnlyCollection<long> ScopedOrganizationIds => [];
        public bool CanAccessOrganization(long organizationId) => true;
        public Result EnsureCanAccessOrganization(long organizationId) => Result.Success();
        public AdminOrganizationScope ResolveOrganizationFilter(long? requestedOrganizationId)
            => new(true, true, requestedOrganizationId, []);
    }

    private sealed class NoopAuditService : IAuditService
    {
        public Task RecordAsync(string actionCode, string entityTypeCode, string entityId, string? detailsJson = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RecordSchoolActionAsync(SchoolAuditContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
