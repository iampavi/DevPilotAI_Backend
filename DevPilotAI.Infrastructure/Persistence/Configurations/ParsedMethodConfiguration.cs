using System.Collections.Generic;
using System.Text.Json;
using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevPilotAI.Infrastructure.Persistence.Configurations;

public class ParsedMethodConfiguration : IEntityTypeConfiguration<ParsedMethod>
{
    public void Configure(EntityTypeBuilder<ParsedMethod> builder)
    {
        builder.ToTable("ParsedMethods");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(m => m.ReturnType)
            .HasMaxLength(200);

        builder.Property(m => m.AccessModifier)
            .HasMaxLength(50)
            .IsRequired();

        // Serializations
        builder.Property(m => m.Parameters)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null!) ?? new List<string>())
            .HasColumnType("nvarchar(max)");

        builder.Property(m => m.Attributes)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null!) ?? new List<string>())
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(m => m.ParsedClassId);

        // Relation: ParsedClass -> ParsedMethods
        builder.HasOne(m => m.ParsedClass)
            .WithMany(c => c.Methods)
            .HasForeignKey(m => m.ParsedClassId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
