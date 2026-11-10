using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.Property(u => u.FullName)
            .IsRequired()
            .HasMaxLength(DomainConstants.Auth.FullNameMaxLength);

        builder.HasIndex(u => u.TenantId);
        builder.HasIndex(u => u.ApartmentId);

        builder.HasOne<Apartment>()
            .WithMany()
            .HasForeignKey(u => u.ApartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
