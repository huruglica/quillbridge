using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using quillbridge.Domain.Models;

namespace quillbridge.Persistence.Configurations;

public class ProspectConfiguration : IEntityTypeConfiguration<Prospect>
{
    public void Configure(EntityTypeBuilder<Prospect> builder)
    {
        builder.ToTable("Prospects");

        builder.HasIndex(p => p.ProspectId).IsUnique();

        builder.Property(p => p.ProspectId).HasMaxLength(20).IsRequired();
        builder.Property(p => p.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(p => p.LastName).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Title).HasMaxLength(200);
        builder.Property(p => p.Company).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Industry).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Hq).HasMaxLength(200).IsRequired();
        builder.Property(p => p.AccountingSystem).HasMaxLength(100);
        builder.Property(p => p.InvoiceProcess).HasMaxLength(100);
        builder.Property(p => p.LeadSource).HasMaxLength(100).IsRequired();
        builder.Property(p => p.LastTouchType).HasMaxLength(100);
        builder.Property(p => p.LastTouchNotes).HasMaxLength(500);
        builder.Property(p => p.DoNotCallReason).HasMaxLength(500);

        builder.HasOne(p => p.Sdr)
            .WithMany()
            .HasForeignKey(p => p.SdrId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
