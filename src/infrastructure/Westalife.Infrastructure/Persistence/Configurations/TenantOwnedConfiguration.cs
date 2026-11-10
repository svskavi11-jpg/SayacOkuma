using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

/// <summary>Kök tablolarda TenantId zorunlu, indekslidir ve site silinince kayıtlar silinmez.</summary>
public static class TenantOwnedConfiguration
{
    public static void Configure<TEntity>(EntityTypeBuilder<TEntity> builder)
        where TEntity : class
    {
        builder.Property<Guid>("TenantId").IsRequired();
        builder.HasIndex("TenantId");
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey("TenantId")
            .OnDelete(DeleteBehavior.Restrict);
    }
}