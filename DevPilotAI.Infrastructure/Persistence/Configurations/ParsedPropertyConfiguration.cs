using System.Collections.Generic;
using System.Text.Json;
using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevPilotAI.Infrastructure.Persistence.Configurations;

public class ParsedPropertyConfiguration : IEntityTypeConfiguration<ParsedProperty>
{
    public void Configure(EntityTypeBuilder<ParsedProperty> builder)
    {
        builder.ToTable("ParsedProperties");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.Type)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.AccessModifier)
            .HasMaxLength(50)
            .IsRequired();

        // Serializations
        builder.Property(p => p.Attributes)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null!) ?? new List<string>())
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(p => p.ParsedClassId);

        // Relation: ParsedClass -> ParsedProperties
        builder.HasOne(p => p.ParsedClass)
            .WithMany(c => c.Properties)
            .HasForeignKey(p => p.ParsedClassId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
