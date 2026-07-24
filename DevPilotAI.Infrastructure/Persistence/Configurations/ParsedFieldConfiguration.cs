using System.Collections.Generic;
using System.Text.Json;
using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevPilotAI.Infrastructure.Persistence.Configurations;

public class ParsedFieldConfiguration : IEntityTypeConfiguration<ParsedField>
{
    public void Configure(EntityTypeBuilder<ParsedField> builder)
    {
        builder.ToTable("ParsedFields");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(f => f.Type)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(f => f.AccessModifier)
            .HasMaxLength(50)
            .IsRequired();

        // Serializations
        builder.Property(f => f.Attributes)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null!) ?? new List<string>())
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(f => f.ParsedClassId);

        // Relation: ParsedClass -> ParsedFields
        builder.HasOne(f => f.ParsedClass)
            .WithMany(c => c.Fields)
            .HasForeignKey(f => f.ParsedClassId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
