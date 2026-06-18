using System;
using FluentAssertions;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests;

public sealed class TopUpDomainTests
{
    [Fact]
    public void TopUpCampaign_Create_ShouldInitializeCorrectly()
    {
        var now = DateTime.UtcNow;
        var campaign = TopUpCampaign.Create(
            organizationId: 10,
            campaignCode: "TEST-01",
            campaignName: "Test Campaign",
            description: "Desc",
            recipientModeCode: "FIXED_SELECTION",
            defaultTopUpAmount: 50.0m,
            reason: "Subsidies",
            scheduleTypeCode: "IMMEDIATE",
            startDate: new DateOnly(2026, 1, 1),
            endDate: null,
            frequencyCode: null,
            frequencyInterval: null,
            currentUserId: 99,
            nowUtc: now
        );

        campaign.OrganizationId.Should().Be(10);
        campaign.CampaignStatusCode.Should().Be("DRAFT");
        campaign.CampaignVersion.Should().Be(1);
        campaign.DefaultTopUpAmount.Should().Be(50.0m);
    }

    [Fact]
    public void TopUpCampaign_Update_ShouldIncrementVersion()
    {
        var now = DateTime.UtcNow;
        var campaign = TopUpCampaign.Create(10, "TEST", "Name", null, "FIXED", 50, "Reason", "IMMEDIATE", new DateOnly(2026, 1, 1), null, null, null, 99, now);

        campaign.Update("New Name", "New Desc", 100, "New Reason", "IMMEDIATE", new DateOnly(2026, 1, 1), null, null, null, 99, now);

        campaign.CampaignVersion.Should().Be(2);
        campaign.CampaignName.Should().Be("New Name");
        campaign.DefaultTopUpAmount.Should().Be(100);
    }

    [Fact]
    public void TopUpRun_UpdateProgress_ShouldAccumulateCorrectly()
    {
        var now = DateTime.UtcNow;
        var campaign = TopUpCampaign.Create(10, "TEST", "Name", null, "FIXED", 50, "Reason", "IMMEDIATE", new DateOnly(2026, 1, 1), null, null, null, 99, now);
        var run = TopUpRun.CreateManual(campaign, "IDEM-1", 99, now, null);

        run.StartProcessing(now);

        // UpdateProgress is gone, we use Finalize now to test state machine transition
        run.Finalize(
            totalProcessed: 6,
            totalSucceeded: 5,
            totalFailed: 1,
            totalSkipped: 0,
            totalAmount: 250m,
            utcNow: now);

        run.TotalSucceeded.Should().Be(5);
        run.TotalFailed.Should().Be(1);
        run.TotalProcessed.Should().Be(6);
        run.TotalAmount.Should().Be(250m);
        run.RunStatusCode.Should().Be(TopUpRunStatusCodes.Partial);
    }

    [Fact]
    public void AccountTransaction_Create_ShouldUpdateBalance()
    {
        var now = DateTime.UtcNow;
        var tx = AccountTransaction.Create(
            educationAccountId: 100,
            transactionTypeCode: "CREDIT",
            amount: 50.5m,
            referenceTypeCode: "TOPUP",
            referenceId: 1,
            idempotencyKey: "IDEM-1",
            currentBalance: 100.0m,
            description: "Test",
            createdByUserId: 99,
            nowUtc: now
        );

        tx.BalanceAfter.Should().Be(150.5m);
        tx.Amount.Should().Be(50.5m);
    }

    [Fact]
    public void EducationAccount_UpdateBalance_ShouldModifyCachedBalance()
    {
        var account = EducationAccount.OpenManual(1, "EA-001", DateTimeOffset.UtcNow, "Reason", "Remarks", 99).Value;
        
        account.CachedBalance.Should().Be(0);
        
        account.UpdateBalance(50m);
        account.CachedBalance.Should().Be(50m);

        account.UpdateBalance(-20m);
        account.CachedBalance.Should().Be(30m);
    }
}
