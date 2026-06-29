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
            deliveryTypeCode: "INSTANT",
            maxTotalAmount: 50,
            currentUserId: 99,
            nowUtc: now
        );

        campaign.OrganizationId.Should().Be(10);
        campaign.CampaignStatusCode.Should().Be("DRAFT");
        campaign.CampaignVersion.Should().Be(1);
        campaign.DefaultTopUpAmount.Should().Be(50.0m);
        campaign.DeliveryTypeCode.Should().Be("INSTANT");
        campaign.MaxTotalAmount.Should().Be(50);
    }

    [Fact]
    public void TopUpCampaign_Update_ShouldIncrementVersion()
    {
        var now = DateTime.UtcNow;
        var campaign = TopUpCampaign.Create(10, "TEST", "Name", null, "FIXED", 50, "Reason", "IMMEDIATE", new DateOnly(2026, 1, 1), null, null, null, "INSTANT", 50, 99, now);

        var result = campaign.Update("New Name", "New Desc", 100, "New Reason", "IMMEDIATE", new DateOnly(2026, 1, 1), null, null, null, "INSTANT", 100, 99, now);
        result.IsSuccess.Should().BeTrue();
        campaign.CampaignVersion.Should().Be(2);
        campaign.CampaignName.Should().Be("New Name");
        campaign.DefaultTopUpAmount.Should().Be(100);
    }

    [Fact]
    public void TopUpRun_UpdateProgress_ShouldAccumulateCorrectly()
    {
        var now = DateTime.UtcNow;
        var campaign = TopUpCampaign.Create(10, "TEST", "Name", null, "FIXED", 50, "Reason", "IMMEDIATE", new DateOnly(2026, 1, 1), null, null, null, "INSTANT", 50, 99, now);
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

    [Fact]
    public void FixedContract_FailedPayment_ShouldStayActiveAndNotAdvanceNextPaymentDate()
    {
        var now = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var contract = DynamicTopUpContract.Create(
            campaignId: 1,
            accountId: 100,
            deliveryTypeCode: DeliveryType.FixedContract,
            amountPerPayment: 300m,
            maxTotalAmount: 900m,
            frequencyCode: "MONTHLY",
            frequencyInterval: 1,
            qualifiedAtUtc: now,
            nextPaymentDate: now);

        contract.ContractStatus.Should().Be(ContractStatuses.Active);
        contract.NextPaymentDate.Should().Be(now);

        var payResult = contract.RecordPayment(300m, now);
        payResult.IsSuccess.Should().BeTrue();
        contract.TotalReceived.Should().Be(300m);
        contract.CyclesCompleted.Should().Be(1);

        var scheduledNext = now.AddMonths(1);
        contract.SetNextPaymentDate(scheduledNext, now);

        var nextRun = now.AddMonths(1);
        contract.CanPayAt(nextRun).Should().BeTrue();

        contract.ContractStatus.Should().Be(ContractStatuses.Active);
        contract.NextPaymentDate.Should().Be(scheduledNext);

        var savedNextPaymentDate = contract.NextPaymentDate;

        contract.SetNextPaymentDate(savedNextPaymentDate, now);

        contract.ContractStatus.Should().Be(ContractStatuses.Active);
        contract.NextPaymentDate.Should().Be(savedNextPaymentDate);
        contract.CanPayAt(nextRun.AddMinutes(1)).Should().BeTrue();
    }

    [Fact]
    public void TopUpCampaign_CannotChangeMaxTotalAmountAfterActivation()
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
            deliveryTypeCode: "INSTANT",
            maxTotalAmount: 50,
            currentUserId: 99,
            nowUtc: now
        );

        var activateResult = campaign.ChangeStatus(TopUpCampaignStatusCodes.Active, 99, now);
        activateResult.IsSuccess.Should().BeTrue();
        campaign.CampaignVersion.Should().Be(2);

        var updateResult = campaign.Update(
            campaignName: "Test Campaign",
            description: "Desc",
            defaultTopUpAmount: 50.0m,
            reason: "Subsidies",
            scheduleTypeCode: "IMMEDIATE",
            startDate: new DateOnly(2026, 1, 1),
            endDate: null,
            frequencyCode: null,
            frequencyInterval: null,
            deliveryTypeCode: "INSTANT",
            maxTotalAmount: 75m,
            currentUserId: 99,
            nowUtc: now
        );

        updateResult.IsFailure.Should().BeTrue();
        updateResult.Error.Code.Should().Be("TopUp.CannotChangeMaxTotalAmountAfterActive");
        campaign.MaxTotalAmount.Should().Be(50m);
        campaign.CampaignVersion.Should().Be(2);
    }

    [Fact]
    public void TopUpCampaign_CannotUpdateAfterActivation_WithSameMaxTotalAmount()
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
            deliveryTypeCode: "INSTANT",
            maxTotalAmount: 50,
            currentUserId: 99,
            nowUtc: now
        );

        var activateResult = campaign.ChangeStatus(TopUpCampaignStatusCodes.Active, 99, now);
        activateResult.IsSuccess.Should().BeTrue();
        campaign.CampaignVersion.Should().Be(2);

        var updateResult = campaign.Update(
            campaignName: "Updated Name",
            description: "Updated Desc",
            defaultTopUpAmount: 50.0m,
            reason: "Subsidies",
            scheduleTypeCode: "IMMEDIATE",
            startDate: new DateOnly(2026, 1, 1),
            endDate: null,
            frequencyCode: null,
            frequencyInterval: null,
            deliveryTypeCode: "INSTANT",
            maxTotalAmount: 50m,
            currentUserId: 99,
            nowUtc: now
        );

        updateResult.IsFailure.Should().BeTrue();
        updateResult.Error.Code.Should().Be("TopUp.CannotUpdateActiveCampaign");
        campaign.CampaignName.Should().Be("Test Campaign");
        campaign.CampaignVersion.Should().Be(2);
    }
}
