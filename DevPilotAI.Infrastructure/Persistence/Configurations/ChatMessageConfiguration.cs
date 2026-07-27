using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevPilotAI.Infrastructure.Persistence.Configurations;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Role)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.Content)
            .IsRequired();

        builder.Property(m => m.Metadata)
            .HasColumnType("nvarchar(max)");

        // Relation: ChatSession -> ChatMessages
        builder.HasOne(m => m.ChatSession)
            .WithMany(s => s.Messages)
            .HasForeignKey(m => m.ChatSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => m.ChatSessionId);
        builder.HasIndex(m => m.CreatedAt);
    }
}
