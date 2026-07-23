using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevPilotAI.Infrastructure.Persistence;

public class ApplicationDbContextInitializer
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ApplicationDbContextInitializer> _logger;

    public ApplicationDbContextInitializer(
        ApplicationDbContext context,
        ILogger<ApplicationDbContextInitializer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            await TrySeedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    private async Task TrySeedAsync()
    {
        // Check if workspaces table is empty
        if (!await _context.Workspaces.AnyAsync())
        {
            _logger.LogInformation("Seeding default workspace...");

            var defaultWorkspace = new Workspace
            {
                Id = Guid.Parse("D035B9FE-B7FE-438B-B0D1-1C349C3AF21E"),
                Name = "Default Workspace",
                Description = "Default system-generated workspace for early projects."
            };

            _context.Workspaces.Add(defaultWorkspace);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Default workspace seeded successfully.");
        }
    }
}
