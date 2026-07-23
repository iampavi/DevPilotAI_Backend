using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevPilotAI.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.Token)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(rt => rt.CreatedByIp)
            .HasMaxLength(100);

        builder.Property(rt => rt.RevokedByIp)
            .HasMaxLength(100);

        builder.Property(rt => rt.ReplacedByToken)
            .HasMaxLength(200);

        builder.Property(rt => rt.ReasonRevoked)
            .HasMaxLength(500);

        // Configure indexes
        builder.HasIndex(rt => rt.Token);
        builder.HasIndex(rt => rt.UserId);

        // One-to-Many Relationship with User
        builder.HasOne(rt => rt.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
