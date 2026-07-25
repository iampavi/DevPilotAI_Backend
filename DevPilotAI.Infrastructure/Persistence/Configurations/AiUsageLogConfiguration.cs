using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevPilotAI.Infrastructure.Persistence.Configurations;

public class AiUsageLogConfiguration : IEntityTypeConfiguration<AiUsageLog>
{
    public void Configure(EntityTypeBuilder<AiUsageLog> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Provider)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(l => l.Model)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(l => l.Cost)
            .HasColumnType("decimal(18, 6)");

        builder.HasIndex(l => l.CreatedAt);
    }
}
