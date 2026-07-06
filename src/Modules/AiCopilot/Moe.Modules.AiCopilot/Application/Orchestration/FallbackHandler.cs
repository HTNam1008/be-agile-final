using Moe.Application.Abstractions.Security;
using Moe.Modules.AiCopilot.Api;
using Moe.Modules.AiCopilot.Application.Security;
using Moe.Modules.AiCopilot.Domain;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

public sealed class FallbackHandler(
    MoeDbContext db,
    SensitiveDataRedactor redactor)
{
    public async Task<Guid> CreateReviewAsync(AiConversation c, long personId, string reason, AiPageContext? page, string transcript, DateTime now, CancellationToken ct)
    {
        AiReviewRecord record = AiReviewRecord.Create(c.Id, personId, reason, page?.Domain ?? "GENERAL", page?.Path, redactor.Redact(transcript), now);
        db.Add(record); await db.SaveChangesAsync(ct); return record.Id;
    }

    public AiHandlerResult FallbackResponse(Guid reviewId)
    {
        return new AiHandlerResult(
            "I cannot answer this reliably right now, so I will not guess. I can help with Education Account balance, bills, payments, refunds, and FAS application guidance. For anything else, the Admin Center can review your case.",
            "FALLBACK",
            new(false, []),
            [],
            FallbackActions(reviewId),
            ReviewRecordId: reviewId)
        {
            FollowUpQuestions =
            [
                "What can you help me with?",
                "How can Admin Center help me?",
                "Show my Education Account balance."
            ]
        };
    }

    public async Task<AiHandlerResult> HandleAsync(AiConversation conversation, AiChatRequest request, AiTurnPlan plan, CancellationToken ct)
    {
        Guid review = await CreateReviewAsync(conversation, conversation.PersonId, "FALLBACK", request.PageContext, request.Message, DateTime.UtcNow, ct);
        return FallbackResponse(review);
    }

    private static AiAction[] FallbackActions(Guid review) =>
        [
            new("NAVIGATE", "Education Account FAQ", "/portal/account"),
            new("NAVIGATE", "Payment FAQ", "/portal/payments"),
            new("NAVIGATE", "FAS FAQ", "/portal/fas"),
            new("CONTACT_ADMIN_CENTER", "Contact Admin Center", Payload: new { reviewRecordId = review })
        ];
}
