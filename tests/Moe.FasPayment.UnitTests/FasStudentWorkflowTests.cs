using FluentAssertions;
using Moe.Modules.FasPayment.Domain.Fas;
using Xunit;

namespace Moe.FasPayment.UnitTests;

public sealed class FasStudentWorkflowTests
{
    [Fact] public void Reject_requires_notes() { var item = FasApplicationScheme.CreateDraft(1, 1, 1, DateTime.UtcNow); item.Submit(); var act = () => item.Reject(1, " ", DateTime.UtcNow); act.Should().Throw<DomainException>(); }
    [Fact] public void Draft_item_cannot_be_activated() { var item = FasApplicationScheme.CreateDraft(1, 1, 1, DateTime.UtcNow); var act = () => item.Activate(DateTime.UtcNow); act.Should().Throw<DomainException>(); }
    [Fact] public void Active_scheme_deactivation_preserves_audit() { var now = DateTime.UtcNow; var active = FasActiveScheme.Activate(1, 1, 1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 10, now); active.Deactivate(11, now.AddMinutes(1), "Replaced"); active.StatusCode.Should().Be("DEACTIVATED"); active.DeactivatedByLoginAccountId.Should().Be(11); active.DeactivatedReason.Should().Be("Replaced"); }
    [Fact] public void Scan_pending_document_can_pass() { var doc = FasDocument.Create(1, "PAYSLIP", "PAYSLIP", true, "a.pdf", "1/a.pdf", "application/pdf", 10, 1, DateTime.UtcNow, true); doc.MarkScanPassed(); doc.UploadStatusCode.Should().Be("SCAN_PASSED"); }
    [Fact] public void Income_calculates_pci_and_rejects_unknown_employment() { var app = Draft(); app.UpdateIncome(false, "EMPLOYED", 2400, 4, 400, 1, DateTime.UtcNow); app.PerCapitaIncome.Should().Be(700); var act = () => app.UpdateIncome(false, "OTHER", 1, 1, 0, 1, DateTime.UtcNow); act.Should().Throw<DomainException>(); }
    [Fact] public void Submitted_application_can_be_edited() { var app = Draft(); app.SubmitDraft(1, DateTime.UtcNow); app.UpdateEmail("student@example.test", 1, DateTime.UtcNow); app.Email.Should().Be("student@example.test"); }
    [Fact] public void Submitted_application_can_be_withdrawn() { var app = Draft(); app.SubmitDraft(1, DateTime.UtcNow); app.Withdraw(1, DateTime.UtcNow); app.StatusCode.Should().Be("WITHDRAWN"); }
    [Fact] public void Draft_scheme_selection_can_be_withdrawn() { var item = FasApplicationScheme.CreateDraft(1, 1, 1, DateTime.UtcNow); item.Withdraw(); item.StatusCode.Should().Be("CANCELLED"); }
    [Fact] public void Pending_scheme_selection_can_be_withdrawn() { var item = FasApplicationScheme.CreateDraft(1, 1, 1, DateTime.UtcNow); item.Submit(); item.Withdraw(); item.StatusCode.Should().Be("CANCELLED"); }
    [Fact] public void Approved_scheme_selection_cannot_be_withdrawn() { var item = FasApplicationScheme.CreateDraft(1, 1, 1, DateTime.UtcNow); item.Submit(); item.Approve(1, 100, null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), DateTime.UtcNow); var act = item.Withdraw; act.Should().Throw<DomainException>(); }
    [Fact] public void Welfare_home_clears_income_fields() { var app = Draft(); app.UpdateIncome(true, null, null, null, 0, 1, DateTime.UtcNow); app.IsWelfareHomeResident.Should().BeTrue(); app.MonthlyHouseholdIncome.Should().BeNull(); app.PerCapitaIncome.Should().BeNull(); }
    private static FasApplication Draft() => FasApplication.CreateDraft("FAS-TEST", 1, 1, "STU-1", "Student", "S****1A", new DateOnly(2005, 1, 1), "SG", "90000000", "Singapore", "student@example.test", 2, "School", "EDUCATION_ACCOUNT", 1, DateTime.UtcNow);
}
