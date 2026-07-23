using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevPilotAI.Infrastructure.Persistence.Configurations;

public class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("Workspaces");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(w => w.Description)
            .HasMaxLength(500);

        // Configure indexes
        builder.HasIndex(w => w.Name);
        builder.HasIndex(w => w.UserId);
        builder.HasIndex(w => w.CreatedAt);
        builder.HasIndex(w => w.LastModifiedAt);

        // One-to-Many Relationship with User
        builder.HasOne(w => w.User)
            .WithMany(u => u.Workspaces)
            .HasForeignKey(w => w.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // One-to-Many Relationship with Projects
        builder.HasMany(w => w.Projects)
            .WithOne(p => p.Workspace)
            .HasForeignKey(p => p.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
