using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class ResidentDocumentConfiguration : IEntityTypeConfiguration<ResidentDocument>
{
    public void Configure(EntityTypeBuilder<ResidentDocument> builder)
    {
        builder.ToTable("ResidentDocuments");
        builder.HasKey(d => d.Id);
        TenantOwnedConfiguration.Configure(builder);
        builder.Property(d => d.FileName).IsRequired().HasMaxLength(DomainConstants.ResidentDocument.FileNameMaxLength);
        builder.Property(d => d.FilePath).IsRequired().HasMaxLength(DomainConstants.ResidentDocument.FilePathMaxLength);
        builder.Property(d => d.DocumentType).IsRequired();
        builder.HasIndex(d => d.ApartmentOwnerId).HasDatabaseName("IX_ResidentDocuments_ApartmentOwnerId");
        builder.HasOne(d => d.ApartmentOwner)
            .WithMany()
            .HasForeignKey(d => d.ApartmentOwnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
