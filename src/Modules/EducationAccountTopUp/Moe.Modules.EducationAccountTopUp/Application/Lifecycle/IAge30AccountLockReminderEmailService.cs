namespace Moe.Modules.EducationAccountTopUp.Application.Lifecycle;

internal interface IAge30AccountLockReminderEmailService
{
    Task SendDueRemindersAsync(DateOnly today, CancellationToken cancellationToken);
}

