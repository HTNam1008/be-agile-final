using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.AiCopilot.Domain;

namespace Moe.Modules.AiCopilot.Infrastructure.Persistence;

public sealed class AiModelConfiguration : IModelConfigurationContributor
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ConversationConfiguration());
        modelBuilder.ApplyConfiguration(new FasSessionConfiguration());
        modelBuilder.ApplyConfiguration(new MessageConfiguration());
        modelBuilder.ApplyConfiguration(new ReviewConfiguration());
        modelBuilder.ApplyConfiguration(new CaseConfiguration());
    }

    private sealed class ConversationConfiguration : IEntityTypeConfiguration<AiConversation>
    {
        public void Configure(EntityTypeBuilder<AiConversation> b)
        {
            b.ToTable("Conversation", "ai"); b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.PersonId, x.UpdatedAtUtc });
            b.Property(x => x.PortalCode).HasMaxLength(30).IsUnicode(false);
            b.Property(x => x.ModeCode).HasMaxLength(30).IsUnicode(false);
            b.Property(x => x.StatusCode).HasMaxLength(30).IsUnicode(false);
            b.Property(x => x.PageContextJson).HasMaxLength(4000);
        }
    }

    private sealed class FasSessionConfiguration : IEntityTypeConfiguration<AiFasSession>
    {
        public void Configure(EntityTypeBuilder<AiFasSession> b)
        {
            b.ToTable("FasSession", "ai");
            b.HasKey(x => x.ConversationId);
            b.HasOne(x => x.Conversation).WithOne(x => x.FasSession)
                .HasForeignKey<AiFasSession>(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Property(x => x.StatusCode).HasMaxLength(30).IsUnicode(false);
            b.Property(x => x.NextQuestion).HasMaxLength(2000);
            b.Property(x => x.CollectedFactsJson).HasMaxLength(8000);
            b.Property(x => x.FormPatchJson).HasMaxLength(8000);
            b.Property(x => x.RowVersion).IsRowVersion();
        }
    }
    private sealed class MessageConfiguration : IEntityTypeConfiguration<AiMessage>
    {
        public void Configure(EntityTypeBuilder<AiMessage> b)
        {
            b.ToTable("Message", "ai"); b.HasKey(x => x.Id); b.Property(x => x.Id).UseIdentityColumn();
            b.HasIndex(x => new { x.ConversationId, x.CreatedAtUtc });
            b.Property(x => x.RoleCode).HasMaxLength(20).IsUnicode(false);
            b.Property(x => x.ContentRedacted).HasMaxLength(4000);
            b.Property(x => x.CitationsJson).HasMaxLength(4000);
            b.Property(x => x.ToolSummaryJson).HasMaxLength(2000);
            b.Property(x => x.ResponseJson).HasMaxLength(8000);
        }
    }
    private sealed class ReviewConfiguration : IEntityTypeConfiguration<AiReviewRecord>
    {
        public void Configure(EntityTypeBuilder<AiReviewRecord> b)
        {
            b.ToTable("ReviewRecord", "ai"); b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.StatusCode, x.CreatedAtUtc });
            b.Property(x => x.ReasonCode).HasMaxLength(50).IsUnicode(false);
            b.Property(x => x.DomainCode).HasMaxLength(30).IsUnicode(false);
            b.Property(x => x.SeverityCode).HasMaxLength(20).IsUnicode(false);
            b.Property(x => x.StatusCode).HasMaxLength(20).IsUnicode(false);
            b.Property(x => x.Route).HasMaxLength(300);
            b.Property(x => x.TranscriptRedacted).HasMaxLength(8000);
        }
    }
    private sealed class CaseConfiguration : IEntityTypeConfiguration<AdminCenterCase>
    {
        public void Configure(EntityTypeBuilder<AdminCenterCase> b)
        {
            b.ToTable("AdminCenterCase", "ai"); b.HasKey(x => x.Id); b.HasIndex(x => new { x.StatusCode, x.CreatedAtUtc });
            b.Property(x => x.DescriptionRedacted).HasMaxLength(2000);
            b.Property(x => x.ContactPreferenceCode).HasMaxLength(20).IsUnicode(false);
            b.Property(x => x.StatusCode).HasMaxLength(20).IsUnicode(false);
        }
    }
}
