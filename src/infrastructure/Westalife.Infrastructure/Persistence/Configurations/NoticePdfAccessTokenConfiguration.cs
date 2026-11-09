using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class NoticePdfAccessTokenConfiguration : IEntityTypeConfiguration<NoticePdfAccessToken>
{
    public void Configure(EntityTypeBuilder<NoticePdfAccessToken> builder)
    {
        builder.ToTable("NoticePdfAccessTokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Token).IsRequired();
        builder.HasIndex(t => t.Token).IsUnique().HasDatabaseName("IX_NoticePdfAccessTokens_Token");
        builder.HasIndex(t => new { t.GasInvoiceId, t.ApartmentId })
            .HasDatabaseName("IX_NoticePdfAccessTokens_Invoice_Apartment");
        builder.Property(t => t.ExpiresAt).IsRequired();
    }
}
