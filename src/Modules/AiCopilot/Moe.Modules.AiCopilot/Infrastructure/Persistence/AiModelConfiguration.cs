using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.AiCopilot.Domain;

namespace Moe.Modules.AiCopilot.Infrastructure.Persistence;

public sealed class AiModelConfiguration : IModelConfigurationContributor
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ReviewConfiguration());
        modelBuilder.ApplyConfiguration(new CaseConfiguration());
        modelBuilder.ApplyConfiguration(new KnowledgeDocumentConfiguration());
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

    private sealed class KnowledgeDocumentConfiguration : IEntityTypeConfiguration<AiKnowledgeDocument>
    {
        public void Configure(EntityTypeBuilder<AiKnowledgeDocument> b)
        {
            b.ToTable("KnowledgeDocument", "ai");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.Domain, x.Status });
            b.Property(x => x.Id).HasMaxLength(100).IsUnicode(false);
            b.Property(x => x.Title).HasMaxLength(500);
            b.Property(x => x.Section).HasMaxLength(500);
            b.Property(x => x.Domain).HasMaxLength(30).IsUnicode(false);
            b.Property(x => x.Status).HasMaxLength(30).IsUnicode(false);
            b.Property(x => x.Version).HasMaxLength(20).IsUnicode(false);
            b.Property(x => x.Url).HasMaxLength(500);
            b.Property(x => x.ReviewOwner).HasMaxLength(200);
            b.Property(x => x.SynonymsJson).HasMaxLength(2000).HasColumnName("Synonyms");
            b.Property(x => x.AllowedIntentsJson).HasMaxLength(1000).HasColumnName("AllowedIntents");
            b.Property(x => x.FollowUpsJson).HasMaxLength(1000).HasColumnName("FollowUps");
            b.Property(x => x.EmbeddingJson).HasColumnName("Embedding").HasColumnType("nvarchar(max)");
            b.Ignore(x => x.Synonyms);
            b.Ignore(x => x.AllowedIntents);
            b.Ignore(x => x.FollowUps);
            b.Ignore(x => x.Embedding);
        }
    }
}
