using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using quillbridge.Domain.Models;

namespace quillbridge.Persistence.Configurations;

public class GuideConfiguration : IEntityTypeConfiguration<Guide>
{
    public void Configure(EntityTypeBuilder<Guide> builder)
    {
        builder.ToTable("Guides");

        // one guide per prospect, input, prompt version and language; Revision separates regenerations
        builder.HasIndex(g => new { g.ProspectId, g.InputHash, g.PromptVersion, g.Language, g.Revision }).IsUnique();
        builder.HasIndex(g => new { g.ProspectId, g.CreatedAt });

        builder.Property(g => g.InputHash).HasMaxLength(64).IsRequired();
        builder.Property(g => g.PromptVersion).HasMaxLength(30).IsRequired();
        builder.Property(g => g.Language).HasMaxLength(10).IsRequired();
        builder.Property(g => g.Source).HasConversion<string>().HasMaxLength(20);
        builder.Property(g => g.Model).HasMaxLength(100);
        builder.Property(g => g.FallbackReason).HasMaxLength(1000);
        builder.Property(g => g.OpeningAngle).HasMaxLength(30);
        builder.Property(g => g.Role).HasMaxLength(30);
        builder.Property(g => g.Content).IsRequired();
        builder.Property(g => g.CostUsd).HasPrecision(18, 6);
        builder.Property(g => g.Outcome).HasConversion<string>().HasMaxLength(30);
        builder.Property(g => g.PushbackHeard).HasMaxLength(50);
        builder.Property(g => g.FeedbackNote).HasMaxLength(1000);

        // guides are history of the prospect and go with it
        builder.HasOne(g => g.Prospect)
            .WithMany()
            .HasForeignKey(g => g.ProspectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
